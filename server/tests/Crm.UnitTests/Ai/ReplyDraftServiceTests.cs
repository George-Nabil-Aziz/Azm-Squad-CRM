using Crm.Application.Ai;
using Crm.Application.Common.Exceptions;
using Crm.Application.KnowledgeBase;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Ai;

internal sealed class FakeKbRetriever : IKbRetriever
{
    public List<KbRetrievedArticle> Articles { get; } = [];

    public List<(string? Text, int Max, string Language)> Calls { get; } = [];

    public Task<IReadOnlyList<KbRetrievedArticle>> FindAsync(string? text, int max, string language, CancellationToken cancellationToken)
    {
        Calls.Add((text, max, language));
        return Task.FromResult<IReadOnlyList<KbRetrievedArticle>>([.. Articles.Take(max)]);
    }
}

public class ReplyDraftServiceTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeTicketMessageRepository _messages = new();
    private readonly FakeKbRetriever _kb = new();
    private readonly FakeAiTextService _ai = new() { Answer = "Hello, please use the reset link." };
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly ReplyDraftService _service;
    private readonly Ticket _ticket;
    private int _seconds;

    public ReplyDraftServiceTests()
    {
        _service = new ReplyDraftService(_tickets, _messages, _kb, _ai);
        var customerId = _tickets.AddCustomer("Nour Trading");
        _ticket = Ticket.Create(customerId, "Cannot sign in", "I forgot my password", null,
            TicketPriority.Mid, TicketChannel.Email, null, _clock.UtcNow.UtcDateTime);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
    }

    private DateTime Next() => _clock.UtcNow.UtcDateTime.AddSeconds(_seconds++);

    private void Customer(string body) => _messages.Add(TicketMessage.Inbound(_ticket.Id, body, TicketChannel.Email, null, Next()));

    private void Agent(string body, bool internalNote = false) =>
        _messages.Add(TicketMessage.Staff(_ticket.Id, body, internalNote, TicketChannel.Email, AgentId, Next()));

    private Task<ReplyDraftResponse> SuggestAsync() => _service.SuggestAsync(_ticket.Id, CancellationToken.None);

    [Fact]
    public async Task TheDraft_UsesTheThreadAndTheKnowledgeBase_AndNamesItsSources()
    {
        Customer("Please help me sign in, I lost my password.");
        var articleId = Guid.NewGuid();
        _kb.Articles.Add(new KbRetrievedArticle(articleId, "Reset your password", "Use the reset link on the sign-in page.", 12));

        var result = await SuggestAsync();

        Assert.Equal("Hello, please use the reset link.", result.Draft);
        Assert.Equal([new ReplyDraftSource(articleId, "Reset your password")], result.Articles);
        var prompt = _ai.Requests.Single().User;
        Assert.Contains("Please help me sign in, I lost my password.", prompt);
        Assert.Contains("Reset your password", prompt);
        Assert.Contains("Use the reset link on the sign-in page.", prompt);
        var (text, max, _) = Assert.Single(_kb.Calls);
        Assert.Equal(3, max);
        Assert.Contains("Cannot sign in", text);
        Assert.Contains("lost my password", text);
    }

    [Fact]
    public async Task InternalNotes_AreNeverSentToTheAi()
    {
        Customer("Where is my refund?");
        Agent("VIP customer, be careful with the budget.", internalNote: true);
        Agent("We are checking.");

        await SuggestAsync();

        var prompt = _ai.Requests.Single().User;
        Assert.DoesNotContain("VIP customer", prompt);
        Assert.Contains("Agent: We are checking.", prompt);
    }

    [Fact]
    public async Task TheLanguage_FollowsTheCustomersLastMessage()
    {
        Customer("I cannot sign in to my account");
        Agent("Please try the reset link.");
        Customer("لم ينجح ذلك، ما زلت لا أستطيع الدخول إلى حسابي");

        var arabic = await SuggestAsync();
        Customer("Sorry, it works now, thank you very much for the help");
        var english = await SuggestAsync();

        Assert.Equal("ar", arabic.Language);
        Assert.Contains("Arabic", _ai.Requests[0].System);
        Assert.Equal("en", english.Language);
        Assert.Contains("English", _ai.Requests[1].System);
        Assert.Equal("ar", _kb.Calls[0].Language);
    }

    [Fact]
    public async Task WithoutCustomerMessages_TheDescriptionThenTheSubjectDecideTheLanguage()
    {
        var customerId = _tickets.AddCustomer("Another");
        var arabic = Ticket.Create(customerId, "مشكلة", "لا أستطيع تسجيل الدخول إلى حسابي", null, TicketPriority.Mid, TicketChannel.Manual, null, Next());
        arabic.AssignNumber(2);
        _tickets.Tickets.Add(arabic);

        var result = await _service.SuggestAsync(arabic.Id, CancellationToken.None);

        Assert.Equal("ar", result.Language);
    }

    [Fact]
    public async Task EmailsAndPhones_AreMasked_AndTheCustomerNameIsNotSent()
    {
        Customer("Call me on 0501234567 or write to nour@corp.example");

        await SuggestAsync();

        var sent = _ai.Requests.Single().User + _ai.Requests.Single().System;
        Assert.DoesNotContain("0501234567", sent);
        Assert.DoesNotContain("nour@corp.example", sent);
        Assert.DoesNotContain("Nour Trading", sent);
    }

    [Fact]
    public async Task WithoutArticles_TheDraftIsStillMade_AndThePromptSaysSo()
    {
        Customer("Hello?");

        var result = await SuggestAsync();

        Assert.Empty(result.Articles);
        Assert.Contains("No knowledge base article", _ai.Requests.Single().User);
    }

    [Fact]
    public async Task Nothing_IsSentOrSaved()
    {
        Customer("Hello?");

        await SuggestAsync();

        Assert.Single(_messages.Messages);
        Assert.Equal(0, _tickets.SaveCount);
    }

    [Fact]
    public async Task AiFailureAndNotConfigured_Propagate()
    {
        _ai.Failure = new AiFailedException("boom");
        await Assert.ThrowsAsync<AiFailedException>(SuggestAsync);

        _ai.Failure = null;
        _ai.IsConfigured = false;
        await Assert.ThrowsAsync<AiNotConfiguredException>(SuggestAsync);
    }

    [Fact]
    public async Task AnEmptyAnswer_IsAFailure()
    {
        _ai.Answer = " ";

        await Assert.ThrowsAsync<AiFailedException>(SuggestAsync);
    }

    [Fact]
    public async Task UnknownTicket_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SuggestAsync(Guid.NewGuid(), CancellationToken.None));
}
