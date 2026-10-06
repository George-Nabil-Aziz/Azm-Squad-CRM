using Crm.Application.Common.Exceptions;
using Crm.Application.Portal;
using Crm.Application.Tickets;
using Crm.Domain.Customers;
using Crm.Domain.Portal;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Portal;

internal sealed class FakeSurveyRepository : ISurveyRepository
{
    public List<TicketSurvey> Surveys { get; } = [];

    public Task<TicketSurvey?> FindByTokenAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(Surveys.FirstOrDefault(s => s.Token == token));

    public Task<TicketSurvey?> FindByTicketAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(Surveys.FirstOrDefault(s => s.TicketId == ticketId));

    public void Add(TicketSurvey survey) => Surveys.Add(survey);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class SurveyServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeSurveyRepository _surveys = new();
    private readonly RecordingSender _sender = new();
    private readonly FakeCustomers _customers = new();
    private readonly TestClock _clock = new(Start);
    private readonly Customer _customer;
    private readonly Ticket _ticket;
    private readonly SurveyService _service;

    public SurveyServiceTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _customer = Customer.Create("Nour Trading", "nour@customer.example", null, Start.UtcDateTime);
        _customers.Customers.Add(_customer);
        _tickets.Customers[_customer.Id] = _customer.Name;
        _tickets.AllCustomerNames[_customer.Id] = _customer.Name;
        _ticket = Ticket.Create(_customer.Id, "Printer", null, null, TicketPriority.Mid, TicketChannel.Portal, null, Start.UtcDateTime);
        _ticket.AssignNumber(7);
        _tickets.Add(_ticket);
        _tickets.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
        _service = new SurveyService(
            _surveys, _tickets, _customers, _sender, new PortalOptions { BaseUrl = "https://help.example.com", SurveyValidDays = 7 }, _clock);
    }

    private string TokenOf() => _surveys.Surveys.Single().Token;

    [Fact]
    public async Task WhenATicketIsResolved_ASurveyIsIssued_AndMailedWithTheLink()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);

        var survey = Assert.Single(_surveys.Surveys);
        Assert.Equal(_ticket.Id, survey.TicketId);
        Assert.Equal(Start.UtcDateTime.AddDays(7), survey.ExpiresAt);
        Assert.True(survey.Token.Length >= 32);
        var mail = Assert.Single(_sender.Sent);
        Assert.Equal("nour@customer.example", mail.Recipient);
        Assert.Contains($"https://help.example.com/portal/survey/{survey.Token}", mail.Body);
        Assert.Contains("[TKT-000007]", mail.Subject);
        Assert.Equal(_ticket.Id, mail.SourceId);
    }

    [Fact]
    public async Task ResolvingAgain_AfterTheCustomerAnswered_SendsNothing()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);
        await _service.SubmitByTokenAsync(TokenOf(), new SubmitFeedbackRequest(5, "great"), CancellationToken.None);

        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);

        Assert.Single(_surveys.Surveys);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task ResolvingAgain_BeforeTheCustomerAnswered_RenewsTheSurvey_AndMailsTheSameLinkAgain()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);
        var token = TokenOf();
        _clock.UtcNow = Start.AddDays(3);

        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);

        var survey = Assert.Single(_surveys.Surveys);
        Assert.Equal(token, survey.Token);
        Assert.Equal(Start.UtcDateTime.AddDays(10), survey.ExpiresAt);
        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task ACustomerWithoutAnEmail_GetsNoMail_ButTheSurveyExistsForThePortal()
    {
        var other = Customer.Create("No Mail", null, "+966501234567", Start.UtcDateTime);
        _customers.Customers.Add(other);
        var ticket = Ticket.Create(other.Id, "Hi", null, null, TicketPriority.Mid, TicketChannel.Portal, null, Start.UtcDateTime);
        ticket.AssignNumber(8);
        _tickets.Add(ticket);
        await _tickets.SaveChangesAsync(CancellationToken.None);

        await _service.OnTicketResolvedAsync(ticket, CancellationToken.None);

        Assert.Single(_surveys.Surveys);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task TheLink_ShowsTheTicketAndTheState()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);

        var info = await _service.GetByTokenAsync(TokenOf(), CancellationToken.None);

        Assert.Equal(("TKT-000007", "Printer", "open"), (info.TicketNumber, info.Subject, info.State));
        Assert.Null(info.Rating);
    }

    [Fact]
    public async Task AnUnknownToken_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByTokenAsync("nope", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SubmitByTokenAsync("nope", new SubmitFeedbackRequest(5, null), CancellationToken.None));
    }

    [Fact]
    public async Task ARating_IsSavedOnce_WithTheComment()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);

        var result = await _service.SubmitByTokenAsync(TokenOf(), new SubmitFeedbackRequest(4, "  Fast help  "), CancellationToken.None);

        Assert.Equal(("answered", 4, "Fast help"), (result.State, result.Rating, result.Comment));
        Assert.Equal(4, _surveys.Surveys[0].Rating);
        Assert.Equal(Start.UtcDateTime, _surveys.Surveys[0].RatedAt);
    }

    [Fact]
    public async Task ASecondSubmit_ThrowsValidation_AndKeepsTheFirstRating()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);
        await _service.SubmitByTokenAsync(TokenOf(), new SubmitFeedbackRequest(5, null), CancellationToken.None);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitByTokenAsync(TokenOf(), new SubmitFeedbackRequest(1, null), CancellationToken.None));

        Assert.Contains("rating", error.Errors.Keys);
        Assert.Equal(5, _surveys.Surveys[0].Rating);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-3)]
    public async Task ARatingOutsideOneToFive_ThrowsValidation_OnRating(int? rating)
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitByTokenAsync(TokenOf(), new SubmitFeedbackRequest(rating, null), CancellationToken.None));

        Assert.Contains("rating", error.Errors.Keys);
        Assert.Null(_surveys.Surveys[0].Rating);
    }

    [Fact]
    public async Task ATooLongComment_ThrowsValidation_OnComment()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitByTokenAsync(TokenOf(), new SubmitFeedbackRequest(5, new string('x', 2001)), CancellationToken.None));

        Assert.Contains("comment", error.Errors.Keys);
    }

    [Fact]
    public async Task TheLink_ExpiresAfterSevenDays()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);
        var token = TokenOf();
        _clock.UtcNow = Start.AddDays(7);

        var info = await _service.GetByTokenAsync(token, CancellationToken.None);
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitByTokenAsync(token, new SubmitFeedbackRequest(5, null), CancellationToken.None));

        Assert.Equal("expired", info.State);
        Assert.Contains("token", error.Errors.Keys);
        Assert.Null(_surveys.Surveys[0].Rating);
    }

    [Fact]
    public async Task TheLink_StillWorksJustBeforeSevenDays()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);
        _clock.UtcNow = Start.AddDays(7).AddSeconds(-1);

        var result = await _service.SubmitByTokenAsync(TokenOf(), new SubmitFeedbackRequest(3, null), CancellationToken.None);

        Assert.Equal(3, result.Rating);
    }

    [Fact]
    public async Task ThePortalCustomer_RatesTheirOwnTicket()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);

        var before = await _service.GetForCustomerAsync(_customer.Id, _ticket.Id, CancellationToken.None);
        var after = await _service.SubmitForCustomerAsync(_customer.Id, _ticket.Id, new SubmitFeedbackRequest(5, "ok"), CancellationToken.None);

        Assert.Equal("open", before.State);
        Assert.Equal(("answered", 5), (after.State, after.Rating));
    }

    [Fact]
    public async Task ATicketWithoutASurvey_ShowsStateNone_AndCannotBeRated()
    {
        var info = await _service.GetForCustomerAsync(_customer.Id, _ticket.Id, CancellationToken.None);

        Assert.Equal("none", info.State);
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitForCustomerAsync(_customer.Id, _ticket.Id, new SubmitFeedbackRequest(5, null), CancellationToken.None));
    }

    [Fact]
    public async Task AnotherCustomersTicket_IsNotFound()
    {
        await _service.OnTicketResolvedAsync(_ticket, CancellationToken.None);
        var stranger = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetForCustomerAsync(stranger, _ticket.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.SubmitForCustomerAsync(stranger, _ticket.Id, new SubmitFeedbackRequest(5, null), CancellationToken.None));
    }
}

public class TicketStatusSurveyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private sealed class RecordingSurveys : ISurveyService
    {
        public List<Guid> Resolved { get; } = [];

        public Task OnTicketResolvedAsync(Ticket ticket, CancellationToken cancellationToken)
        {
            Resolved.Add(ticket.Id);
            return Task.CompletedTask;
        }

        public Task<SurveyInfoResponse> GetByTokenAsync(string token, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SurveyInfoResponse> SubmitByTokenAsync(string token, SubmitFeedbackRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SurveyInfoResponse> GetForCustomerAsync(Guid customerId, Guid ticketId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SurveyInfoResponse> SubmitForCustomerAsync(Guid customerId, Guid ticketId, SubmitFeedbackRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task OnlyMovingToResolved_TriggersTheSurvey()
    {
        var categories = new FakeTicketCategoryRepository();
        var tickets = new FakeTicketRepository(categories);
        var customer = tickets.AddCustomer("Nour");
        var ticket = Ticket.Create(customer, "Printer", null, null, TicketPriority.Mid, TicketChannel.Manual, null, Start.UtcDateTime);
        ticket.AssignNumber(1);
        tickets.Add(ticket);
        await tickets.SaveChangesAsync(CancellationToken.None);
        var surveys = new RecordingSurveys();
        var service = new TicketStatusService(tickets, new FakeTicketHistoryRecorder(), new TestClock(Start), surveys);

        await service.ChangeAsync(ticket.Id, new ChangeTicketStatusRequest("open"), CancellationToken.None);
        Assert.Empty(surveys.Resolved);
        await service.ChangeAsync(ticket.Id, new ChangeTicketStatusRequest("resolved"), CancellationToken.None);

        Assert.Equal([ticket.Id], surveys.Resolved);
    }
}
