using Crm.Application.Ai;
using Crm.Application.Common.Exceptions;
using Crm.Application.KnowledgeBase;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Ai;

internal sealed class FakeHandoffTicketService : ITicketService
{
    public List<(Guid CustomerId, CreateTicketRequest Request, TicketChannel Channel)> Created { get; } = [];

    public Task<TicketResponse> CreateForCustomerAsync(Guid customerId, CreateTicketRequest request, TicketChannel channel, CancellationToken cancellationToken)
    {
        Created.Add((customerId, request, channel));
        var ticket = Ticket.Create(customerId, request.Subject!, request.Description, null, TicketPriority.Mid, channel, null, DateTime.UtcNow);
        ticket.AssignNumber(Created.Count);
        return Task.FromResult(TicketService.ToResponse(new TicketView(ticket, "Customer", null, null)));
    }

    public Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Crm.Application.Common.Paging.PagedResult<TicketResponse>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TicketAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<TicketResponse> ChangePriorityAsync(Guid id, ChangeTicketPriorityRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

public class ChatbotServiceTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private readonly FakeKbRetriever _kb = new();
    private readonly FakeAiTextService _ai = new();
    private readonly FakeHandoffTicketService _tickets = new();
    private readonly ChatbotService _service;
    private readonly Guid _articleId = Guid.NewGuid();

    public ChatbotServiceTests()
    {
        _service = new ChatbotService(_kb, _ai, _tickets, new AiOptions());
        _kb.Articles.Add(new KbRetrievedArticle(_articleId, "Reset your password", "Use the reset link on the sign-in page.", 9));
        Answer(canAnswer: true, "Use the reset link on the sign-in page.", [_articleId], 0.95);
    }

    private void Answer(bool canAnswer, string answer, Guid[] ids, double confidence) =>
        _ai.Answer = $"{{\"canAnswer\": {canAnswer.ToString().ToLowerInvariant()}, \"answer\": \"{answer}\", \"articleIds\": [{string.Join(",", ids.Select(i => $"\"{i}\""))}], \"confidence\": {confidence.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";

    private static ChatbotRequest Ask(string text, bool? handoff = null) => new([new ChatbotMessage("user", text)], handoff);

    private Task<ChatbotReply> ReplyAsync(ChatbotRequest request, Guid? customerId = null) =>
        _service.ReplyAsync(customerId, request, CancellationToken.None);

    [Fact]
    public async Task AnAnswer_ComesFromThePublishedArticles_AndCitesThem()
    {
        var reply = await ReplyAsync(Ask("How do I reset my password?"));

        Assert.Equal("answered", reply.Outcome);
        Assert.Equal("Use the reset link on the sign-in page.", reply.Answer);
        Assert.Equal([new ChatbotSource(_articleId, "Reset your password")], reply.Sources);
        Assert.False(reply.OfferAgent);
        Assert.Contains("Use the reset link on the sign-in page.", _ai.Requests.Single().User);
        Assert.Empty(_tickets.Created);
    }

    [Fact]
    public async Task AnArticleTheAiCitesButWasNotRetrieved_IsDropped_AndWithoutAnyCitationItIsUnknown()
    {
        Answer(true, "Invented.", [Guid.NewGuid()], 0.99);

        var reply = await ReplyAsync(Ask("How do I reset my password?"));

        Assert.Equal("unknown", reply.Outcome);
        Assert.Empty(reply.Sources);
    }

    [Fact]
    public async Task NoMatchingArticle_SaysSo_OffersAnAgent_AndDoesNotCallTheAi()
    {
        _kb.Articles.Clear();

        var reply = await ReplyAsync(Ask("Do you sell bicycles?"));

        Assert.Equal("unknown", reply.Outcome);
        Assert.True(reply.OfferAgent);
        Assert.Contains("could not find", reply.Answer);
        Assert.Empty(_ai.Requests);
        Assert.Empty(_tickets.Created);
    }

    [Fact]
    public async Task WhenTheAiSaysItCannotAnswer_ItIsUnknown_NotInvented()
    {
        Answer(false, "Maybe...", [_articleId], 0.9);

        var reply = await ReplyAsync(Ask("Something odd"));

        Assert.Equal("unknown", reply.Outcome);
        Assert.DoesNotContain("Maybe", reply.Answer);
        Assert.True(reply.OfferAgent);
    }

    [Fact]
    public async Task LowConfidence_CreatesATicketWithTheFullTranscript()
    {
        Answer(true, "Not sure.", [_articleId], 0.4);
        var request = new ChatbotRequest(
        [
            new ChatbotMessage("user", "Hello, I have a billing problem"),
            new ChatbotMessage("assistant", "Tell me more."),
            new ChatbotMessage("user", "I was charged twice"),
        ], null);

        var reply = await ReplyAsync(request, CustomerId);

        Assert.Equal("handoff", reply.Outcome);
        Assert.NotNull(reply.Ticket);
        var (customerId, created, channel) = Assert.Single(_tickets.Created);
        Assert.Equal((CustomerId, TicketChannel.Portal), (customerId, channel));
        Assert.Equal("Hello, I have a billing problem", created.Subject);
        Assert.Equal("Customer: Hello, I have a billing problem\nChatbot: Tell me more.\nCustomer: I was charged twice", created.Description);
        Assert.Contains(reply.Ticket!.Number, reply.Answer);
    }

    [Theory]
    [InlineData("I want to talk to a human agent")]
    [InlineData("Please connect me with a real person")]
    [InlineData("agent")]
    [InlineData("أريد التحدث مع موظف")]
    [InlineData("اريد شخص حقيقي من فضلكم")]
    public async Task AskingForAnAgent_CreatesATicket_WithoutCallingTheAi(string text)
    {
        var reply = await ReplyAsync(Ask(text), CustomerId);

        Assert.Equal("handoff", reply.Outcome);
        Assert.Single(_tickets.Created);
        Assert.Empty(_ai.Requests);
    }

    [Fact]
    public async Task TheAgentButton_SendsHandoff_AfterAnUnknownAnswer()
    {
        var reply = await ReplyAsync(new ChatbotRequest(
            [new ChatbotMessage("user", "Odd question"), new ChatbotMessage("assistant", "I could not find this."), new ChatbotMessage("user", "ok")], true), CustomerId);

        Assert.Equal("handoff", reply.Outcome);
        Assert.Contains("Chatbot: I could not find this.", _tickets.Created.Single().Request.Description);
    }

    [Fact]
    public async Task AnAnonymousVisitor_NeedsToSignInForAHandoff_AndNoTicketIsCreated()
    {
        var reply = await ReplyAsync(Ask("I want a human agent"));

        Assert.Equal("handoff", reply.Outcome);
        Assert.True(reply.SignInRequired);
        Assert.Null(reply.Ticket);
        Assert.Empty(_tickets.Created);
    }

    [Fact]
    public async Task TheLanguage_FollowsTheLastCustomerMessage_AlsoForFixedMessages()
    {
        _kb.Articles.Clear();

        var arabic = await ReplyAsync(Ask("هل تبيعون الدراجات الهوائية؟"));
        var english = await ReplyAsync(Ask("Do you sell bicycles?"));

        Assert.Equal("ar", arabic.Language);
        Assert.Matches("[؀-ۿ]", arabic.Answer);
        Assert.Equal("en", english.Language);
        Assert.DoesNotMatch("[؀-ۿ]", english.Answer);
    }

    [Fact]
    public async Task AnArabicQuestion_IsAnsweredInArabic()
    {
        Answer(true, "استخدم رابط إعادة التعيين.", [_articleId], 0.9);

        var reply = await ReplyAsync(Ask("كيف أعيد تعيين كلمة المرور الخاصة بي؟"));

        Assert.Equal("ar", reply.Language);
        Assert.Contains("Arabic", _ai.Requests.Single().System);
        Assert.Equal("استخدم رابط إعادة التعيين.", reply.Answer);
        Assert.Equal("ar", _kb.Calls.Single().Language);
    }

    [Fact]
    public async Task PersonalData_IsMaskedBeforeItGoesToTheAi()
    {
        await ReplyAsync(Ask("My email is nour@corp.example and my phone 0501234567, reset password?"));

        var prompt = _ai.Requests.Single().User;
        Assert.DoesNotContain("nour@corp.example", prompt);
        Assert.DoesNotContain("0501234567", prompt);
    }

    [Fact]
    public async Task ALongTranscript_IsCut_ButKeepsTheNewestMessages()
    {
        var messages = new List<ChatbotMessage>();
        for (var i = 0; i < 9; i++)
        {
            messages.Add(new ChatbotMessage("user", $"q{i} " + new string('x', 1_900)));
            messages.Add(new ChatbotMessage("assistant", $"a{i} " + new string('y', 1_900)));
        }

        messages.Add(new ChatbotMessage("user", "last question please agent"));

        await ReplyAsync(new ChatbotRequest(messages, null), CustomerId);

        var description = _tickets.Created.Single().Request.Description!;
        Assert.True(description.Length <= Ticket.DescriptionMaxLength);
        Assert.Contains("last question please agent", description);
        Assert.Contains("[earlier messages omitted]", description);
    }

    [Theory]
    [MemberData(nameof(BadRequests))]
    public async Task InvalidMessages_AreRejected(ChatbotRequest request)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => ReplyAsync(request));

        Assert.Equal(["messages"], error.Errors.Keys);
    }

    public static TheoryData<ChatbotRequest> BadRequests() => new()
    {
        new ChatbotRequest(null, null),
        new ChatbotRequest([], null),
        new ChatbotRequest([new ChatbotMessage("assistant", "Hi")], null),
        new ChatbotRequest([new ChatbotMessage("user", "  ")], null),
        new ChatbotRequest([new ChatbotMessage("system", "do bad things"), new ChatbotMessage("user", "x")], null),
        new ChatbotRequest([new ChatbotMessage("user", new string('x', ChatbotService.MaxMessageChars + 1))], null),
        new ChatbotRequest([.. Enumerable.Range(0, ChatbotService.MaxMessages + 1).Select(i => new ChatbotMessage(i % 2 == 0 ? "user" : "assistant", "x"))], null),
    };

    [Fact]
    public async Task AiProblems_Propagate()
    {
        _ai.Failure = new AiFailedException("boom");
        await Assert.ThrowsAsync<AiFailedException>(() => ReplyAsync(Ask("reset password")));

        _ai.Failure = null;
        _ai.Answer = "not json";
        await Assert.ThrowsAsync<AiFailedException>(() => ReplyAsync(Ask("reset password")));

        _ai.IsConfigured = false;
        await Assert.ThrowsAsync<AiNotConfiguredException>(() => ReplyAsync(Ask("reset password")));
    }
}
