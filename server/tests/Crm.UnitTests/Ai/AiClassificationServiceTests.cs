using Crm.Application.Ai;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Ai;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Ai;

internal sealed class FakeClassificationRepository : ITicketAiClassificationRepository
{
    public List<TicketAiClassification> Rows { get; } = [];

    public Task<TicketAiClassification?> FindAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.TicketId == ticketId));

    public void Add(TicketAiClassification classification) => Rows.Add(classification);
}

public class AiClassificationServiceTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeClassificationRepository _repository = new();
    private readonly FakeAiTextService _ai = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly AiOptions _options = new();
    private readonly AiClassificationService _service;
    private readonly TicketCategory _billing;
    private readonly TicketCategory _support;
    private readonly TicketCategory _inactive;

    public AiClassificationServiceTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _service = new AiClassificationService(_ai, _categories, _repository, _tickets, new FakeCurrentUser(AgentId), _options);
        _billing = TicketCategory.Create("Billing", _clock.UtcNow.UtcDateTime);
        _support = TicketCategory.Create("Technical support", _clock.UtcNow.UtcDateTime);
        _inactive = TicketCategory.Create("Old category", _clock.UtcNow.UtcDateTime);
        _inactive.Update("Old category", false, _clock.UtcNow.UtcDateTime);
        _categories.Add(_billing);
        _categories.Add(_support);
        _categories.Add(_inactive);
    }

    private Task<AiClassificationOutcome?> ClassifyAsync(string subject = "Invoice is wrong", string? description = "Line 3 is charged twice.") =>
        _service.ClassifyAsync(subject, description, CancellationToken.None);

    private void Answer(Guid? categoryId, string priority, string confidence) =>
        _ai.Answer = $"{{\"categoryId\": {(categoryId is null ? "null" : $"\"{categoryId}\"")}, \"priority\": \"{priority}\", \"confidence\": {confidence}}}";

    [Fact]
    public async Task ASuggestionWithConfidence_IsReturned()
    {
        Answer(_billing.Id, "high", "0.92");

        var outcome = await ClassifyAsync();

        Assert.Equal(new AiClassificationOutcome(_billing.Id, TicketPriority.High, 0.92, true), outcome);
    }

    [Theory]
    [InlineData("0.8", true)]
    [InlineData("0.81", true)]
    [InlineData("1", true)]
    [InlineData("0.79", false)]
    [InlineData("0.2", false)]
    public async Task TheThreshold_DecidesAutomaticApplication(string confidence, bool meets)
    {
        Answer(_billing.Id, "mid", confidence);

        var outcome = await ClassifyAsync();

        Assert.Equal(meets, outcome!.MeetsThreshold);
    }

    [Fact]
    public async Task TheConfiguredThreshold_IsUsed()
    {
        _options.ConfidenceThreshold = 0.5;
        Answer(_billing.Id, "mid", "0.6");

        Assert.True((await ClassifyAsync())!.MeetsThreshold);
    }

    [Fact]
    public async Task OnlyActiveCategories_AreOffered_AndPersonalDataIsMasked()
    {
        Answer(_billing.Id, "mid", "0.9");

        await ClassifyAsync("Call 0501234567", "Mail nour@corp.example about the invoice");

        var request = Assert.Single(_ai.Requests);
        Assert.Contains(_billing.Id.ToString(), request.User);
        Assert.Contains("Technical support", request.User);
        Assert.DoesNotContain("Old category", request.User);
        Assert.DoesNotContain("0501234567", request.User);
        Assert.DoesNotContain("nour@corp.example", request.User);
        Assert.Contains("JSON", request.System);
    }

    [Fact]
    public async Task AnUnknownOrInactiveCategory_BecomesNoCategory()
    {
        Answer(Guid.NewGuid(), "low", "0.95");
        Assert.Null((await ClassifyAsync())!.CategoryId);

        Answer(_inactive.Id, "low", "0.95");
        Assert.Null((await ClassifyAsync())!.CategoryId);
    }

    [Fact]
    public async Task JsonInsideTextOrACodeFence_IsAccepted()
    {
        _ai.Answer = $"Here you go:\n```json\n{{\"categoryId\": \"{_support.Id}\", \"priority\": \"LOW\", \"confidence\": \"0.85\"}}\n```";

        var outcome = await ClassifyAsync();

        Assert.Equal(new AiClassificationOutcome(_support.Id, TicketPriority.Low, 0.85, true), outcome);
    }

    [Fact]
    public async Task AConfidenceOutsideZeroToOne_IsClamped()
    {
        Answer(_billing.Id, "high", "7");
        Assert.Equal(1, (await ClassifyAsync())!.Confidence);

        Answer(_billing.Id, "high", "-3");
        Assert.Equal(0, (await ClassifyAsync())!.Confidence);
    }

    [Theory]
    [InlineData("I cannot decide.")]
    [InlineData("")]
    [InlineData("{ not json }")]
    [InlineData("{\"categoryId\": null, \"priority\": \"urgent\", \"confidence\": 0.9}")]
    [InlineData("{\"categoryId\": null, \"priority\": \"high\"}")]
    [InlineData("[1, 2]")]
    public async Task AnAnswerThatCannotBeUsed_MeansUnavailable(string answer)
    {
        _ai.Answer = answer;

        Assert.Null(await ClassifyAsync());
    }

    [Fact]
    public async Task WhenAiIsUnavailable_NoOutcomeAndNoError()
    {
        _ai.IsConfigured = false;
        Assert.Null(await ClassifyAsync());
        Assert.Empty(_ai.Requests);

        _ai.IsConfigured = true;
        _ai.Failure = new AiFailedException("boom");
        Assert.Null(await ClassifyAsync());

        _ai.Failure = new OperationCanceledException(); // our own time limit
        Assert.Null(await ClassifyAsync());
    }

    [Fact]
    public async Task ACancelledCaller_IsStillCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _ai.Failure = new OperationCanceledException(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.ClassifyAsync("s", null, cts.Token));
    }

    [Fact]
    public async Task WithoutActiveCategories_OnlyThePriorityIsSuggested()
    {
        _categories.Categories.Clear();
        _ai.Answer = "{\"categoryId\": null, \"priority\": \"high\", \"confidence\": 0.9}";

        var outcome = await ClassifyAsync();

        Assert.Equal(new AiClassificationOutcome(null, TicketPriority.High, 0.9, true), outcome);
    }

    [Fact]
    public async Task Save_StoresTheSuggestionWithTheAppliedFlags()
    {
        var ticketId = Guid.NewGuid();
        var outcome = new AiClassificationOutcome(_billing.Id, TicketPriority.High, 0.9, true);

        _service.Save(ticketId, outcome, categoryApplied: true, priorityApplied: false, _clock.UtcNow.UtcDateTime);

        var row = Assert.Single(_repository.Rows);
        Assert.Equal((ticketId, _billing.Id, TicketPriority.High, 0.9, true, false), (row.TicketId, row.SuggestedCategoryId, row.SuggestedPriority, row.Confidence, row.CategoryApplied, row.PriorityApplied));
    }

    private Guid AddClassification(bool applied = true)
    {
        var ticketId = Guid.NewGuid();
        _repository.Add(TicketAiClassification.Create(ticketId, _billing.Id, TicketPriority.High, 0.9, applied, applied, _clock.UtcNow.UtcDateTime));
        return ticketId;
    }

    [Fact]
    public async Task AnAgentChangingTheCategory_IsRecordedAsAnOverride()
    {
        var ticketId = AddClassification();

        await _service.RecordCategoryChangeAsync(ticketId, _support.Id, _clock.UtcNow.UtcDateTime.AddMinutes(3), CancellationToken.None);

        var row = _repository.Rows.Single();
        Assert.Equal((_support.Id, AgentId, _clock.UtcNow.UtcDateTime.AddMinutes(3)), (row.CategoryOverriddenTo, row.OverriddenById, row.CategoryOverriddenAt));
    }

    [Fact]
    public async Task AnAgentChangingThePriority_IsRecordedAsAnOverride()
    {
        var ticketId = AddClassification(applied: false); // overriding a suggestion that was only shown counts too

        await _service.RecordPriorityChangeAsync(ticketId, TicketPriority.Low, _clock.UtcNow.UtcDateTime, CancellationToken.None);

        Assert.Equal(TicketPriority.Low, _repository.Rows.Single().PriorityOverriddenTo);
    }

    [Fact]
    public async Task AcceptingTheSuggestion_IsNotAnOverride()
    {
        var ticketId = AddClassification(applied: false);

        await _service.RecordCategoryChangeAsync(ticketId, _billing.Id, _clock.UtcNow.UtcDateTime, CancellationToken.None);

        Assert.Null(_repository.Rows.Single().CategoryOverriddenAt);
    }

    [Fact]
    public async Task ATicketWithoutAClassification_IsLeftAlone()
    {
        await _service.RecordCategoryChangeAsync(Guid.NewGuid(), _support.Id, _clock.UtcNow.UtcDateTime, CancellationToken.None);
        await _service.RecordPriorityChangeAsync(Guid.NewGuid(), TicketPriority.Low, _clock.UtcNow.UtcDateTime, CancellationToken.None);

        Assert.Empty(_repository.Rows);
    }

    private Ticket AddTicket()
    {
        var customerId = _tickets.AddCustomer("Nour");
        var ticket = Ticket.Create(customerId, "Invoice", null, null, TicketPriority.Mid, TicketChannel.Manual, null, _clock.UtcNow.UtcDateTime);
        ticket.AssignNumber(1);
        _tickets.Tickets.Add(ticket);
        return ticket;
    }

    [Fact]
    public async Task Get_ShowsAppliedAndSuggestedResults_WithTheCategoryName()
    {
        var ticket = AddTicket();
        _repository.Add(TicketAiClassification.Create(ticket.Id, _billing.Id, TicketPriority.High, 0.93, true, true, _clock.UtcNow.UtcDateTime));

        var applied = await _service.GetAsync(ticket.Id, CancellationToken.None);

        Assert.Equal("applied", applied.Status);
        Assert.Equal((_billing.Id, "Billing", "high", 0.93), (applied.SuggestedCategoryId, applied.SuggestedCategoryName, applied.SuggestedPriority, applied.Confidence));
        Assert.True(applied.CategoryApplied);
        Assert.True(applied.PriorityApplied);

        _repository.Rows.Clear();
        _repository.Add(TicketAiClassification.Create(ticket.Id, _billing.Id, TicketPriority.High, 0.5, false, false, _clock.UtcNow.UtcDateTime));
        Assert.Equal("suggested", (await _service.GetAsync(ticket.Id, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Get_WithoutAClassification_IsNone_AndAnUnknownTicketIsNotFound()
    {
        var ticket = AddTicket();

        var none = await _service.GetAsync(ticket.Id, CancellationToken.None);

        Assert.Equal("none", none.Status);
        Assert.Null(none.Confidence);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
