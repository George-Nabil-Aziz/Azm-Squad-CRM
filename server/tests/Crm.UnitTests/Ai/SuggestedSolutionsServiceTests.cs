using Crm.Application.Ai;
using Crm.Application.Common.Exceptions;
using Crm.Application.KnowledgeBase;
using Crm.Domain.Ai;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Ai;

internal sealed class FakeSuggestionFeedbackRepository : ISuggestionFeedbackRepository
{
    public HashSet<Guid> Published { get; } = [];

    public List<TicketSuggestionFeedback> Rows { get; } = [];

    public Task<bool> IsPublishedArticleAsync(Guid articleId, CancellationToken cancellationToken) =>
        Task.FromResult(Published.Contains(articleId));

    public Task<TicketSuggestionFeedback?> FindAsync(Guid ticketId, Guid articleId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.TicketId == ticketId && r.ArticleId == articleId && r.UserId == userId));

    public Task<IReadOnlyDictionary<Guid, bool>> ListVotesAsync(Guid ticketId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, bool>>(Rows.Where(r => r.TicketId == ticketId && r.UserId == userId).ToDictionary(r => r.ArticleId, r => r.Useful));

    public void Add(TicketSuggestionFeedback feedback) => Rows.Add(feedback);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class SuggestedSolutionsServiceTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeTicketMessageRepository _messages = new();
    private readonly FakeKbRetriever _kb = new();
    private readonly FakeAiTextService _ai = new() { IsConfigured = false };
    private readonly FakeSuggestionFeedbackRepository _feedback = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly Ticket _ticket;
    private readonly List<Guid> _articles = [];

    public SuggestedSolutionsServiceTests()
    {
        var customerId = _tickets.AddCustomer("Nour");
        _ticket = Ticket.Create(customerId, "Cannot sign in", "Forgot my password", null, TicketPriority.Mid, TicketChannel.Email, null, _clock.UtcNow.UtcDateTime);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
        for (var i = 1; i <= 5; i++)
        {
            var id = Guid.NewGuid();
            _articles.Add(id);
            _kb.Articles.Add(new KbRetrievedArticle(id, $"Article {i}", $"Body of article {i}", 10 - i));
            _feedback.Published.Add(id);
        }
    }

    private SuggestedSolutionsService Service(Guid? userId = null) => new(
        _tickets, _messages, _kb, _ai, _feedback, new FakeCurrentUser(userId ?? AgentId), _clock);

    private Task<IReadOnlyList<SuggestedSolutionResponse>> ListAsync(SuggestedSolutionsService? service = null) =>
        (service ?? Service()).ListAsync(_ticket.Id, CancellationToken.None);

    [Fact]
    public async Task WithoutAi_TheTopThreeByKeywordScore_AreReturned()
    {
        var list = await ListAsync();

        Assert.Equal(_articles.Take(3), list.Select(s => s.ArticleId));
        Assert.Equal("Article 1", list[0].Title);
        Assert.Equal("Body of article 1", list[0].Summary);
        Assert.All(list, s => Assert.Null(s.Useful));
        Assert.Equal(8, _kb.Calls.Single().Max);
    }

    [Fact]
    public async Task WithAi_ItsChoiceAndOrderIsUsed_AndForeignIdsAreIgnored()
    {
        _ai.IsConfigured = true;
        _ai.Answer = $"{{\"articleIds\": [\"{_articles[4]}\", \"{Guid.NewGuid()}\", \"{_articles[1]}\"]}}";

        var list = await ListAsync();

        Assert.Equal([_articles[4], _articles[1]], list.Select(s => s.ArticleId));
        Assert.Contains("Article 5", _ai.Requests.Single().User);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"articleIds\": []}")]
    [InlineData("{\"articleIds\": [\"00000000-0000-0000-0000-000000000009\"]}")]
    public async Task AnUnusableAiAnswer_FallsBackToTheKeywordRanking(string answer)
    {
        _ai.IsConfigured = true;
        _ai.Answer = answer;

        Assert.Equal(_articles.Take(3), (await ListAsync()).Select(s => s.ArticleId));
    }

    [Fact]
    public async Task WhenTheAiFails_TheKeywordRankingIsUsed()
    {
        _ai.IsConfigured = true;
        _ai.Failure = new AiFailedException("boom");

        Assert.Equal(_articles.Take(3), (await ListAsync()).Select(s => s.ArticleId));
    }

    [Fact]
    public async Task AtMostThree_AreReturned_EvenIfTheAiPicksMore()
    {
        _ai.IsConfigured = true;
        _ai.Answer = $"{{\"articleIds\": [{string.Join(",", _articles.Select(a => $"\"{a}\""))}]}}";

        Assert.Equal(3, (await ListAsync()).Count);
    }

    [Fact]
    public async Task APromptDoesNotContainPersonalData()
    {
        _ai.IsConfigured = true;
        _messages.Add(TicketMessage.Inbound(_ticket.Id, "Call 0501234567 or nour@corp.example", TicketChannel.Email, null, _clock.UtcNow.UtcDateTime));

        await ListAsync();

        Assert.DoesNotContain("0501234567", _ai.Requests.Single().User);
        Assert.DoesNotContain("nour@corp.example", _ai.Requests.Single().User);
    }

    [Fact]
    public async Task UnknownTicket_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => Service().ListAsync(Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task Feedback_IsStoredPerSuggestion_AndShownToItsAuthor()
    {
        var service = Service();

        var saved = await service.RecordFeedbackAsync(_ticket.Id, _articles[0], new SuggestionFeedbackRequest(true), CancellationToken.None);
        await service.RecordFeedbackAsync(_ticket.Id, _articles[1], new SuggestionFeedbackRequest(false), CancellationToken.None);

        Assert.Equal(new SuggestionFeedbackResponse(_articles[0], true), saved);
        var list = await ListAsync(service);
        Assert.Equal([true, false, null], list.Select(s => s.Useful));
    }

    [Fact]
    public async Task VotingAgain_ReplacesTheVote_AndOtherUsersVotesAreSeparate()
    {
        await Service().RecordFeedbackAsync(_ticket.Id, _articles[0], new SuggestionFeedbackRequest(true), CancellationToken.None);
        await Service().RecordFeedbackAsync(_ticket.Id, _articles[0], new SuggestionFeedbackRequest(false), CancellationToken.None);
        var other = Service(Guid.NewGuid());
        await other.RecordFeedbackAsync(_ticket.Id, _articles[0], new SuggestionFeedbackRequest(true), CancellationToken.None);

        Assert.Equal(2, _feedback.Rows.Count);
        Assert.False((await ListAsync())[0].Useful);
        Assert.True((await ListAsync(other))[0].Useful);
    }

    [Fact]
    public async Task Feedback_NeedsAVote_APublishedArticle_AndATicket()
    {
        var service = Service();
        var missing = await Assert.ThrowsAsync<ValidationException>(() =>
            service.RecordFeedbackAsync(_ticket.Id, _articles[0], new SuggestionFeedbackRequest(null), CancellationToken.None));
        var draft = await Assert.ThrowsAsync<ValidationException>(() =>
            service.RecordFeedbackAsync(_ticket.Id, Guid.NewGuid(), new SuggestionFeedbackRequest(true), CancellationToken.None));

        Assert.Equal(["useful"], missing.Errors.Keys);
        Assert.Equal(["articleId"], draft.Errors.Keys);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.RecordFeedbackAsync(Guid.NewGuid(), _articles[0], new SuggestionFeedbackRequest(true), CancellationToken.None));
        Assert.Empty(_feedback.Rows);
    }
}
