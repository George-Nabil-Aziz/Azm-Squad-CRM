using Crm.Application.Channels;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.RateLimiting;
using Crm.Application.Tickets;
using Crm.Application.WebForms;
using Crm.Domain.Channels;
using Crm.Domain.Tickets;
using Crm.UnitTests.Channels;
using Crm.UnitTests.Portal;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.WebForms;

public class WebFormServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeCustomerService _customers = new();
    private readonly FakeTicketService _tickets = new();
    private readonly RecordingSender _sender = new();
    private readonly FakeCaptcha _captcha = new();
    private readonly TestClock _clock = new(Start);
    private readonly WebFormOptions _options = new() { RateLimitRequests = 2, RateLimitWindowSeconds = 60 };

    private WebFormService Service(IChannelSender? sender = null) => new(
        _options, new FixedWindowRateLimiter(_clock), _captcha, new WebFormRequestValidator(), _customers, _tickets, sender ?? _sender);

    private sealed class FakeCaptcha : ICaptchaVerifier
    {
        public bool Result { get; set; } = true;

        public List<(string? Token, string? Ip)> Calls { get; } = [];

        public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
        {
            Calls.Add((token, remoteIp));
            return Task.FromResult(Result);
        }
    }

    private sealed class ThrowingSender : IChannelSender
    {
        public Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken) => throw new InvalidOperationException("mail down");

        public Task<bool> ApplyDeliveryStatusAsync(string providerMessageId, DeliveryStatus status, string? error, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task EnsureCanSendAsync(ChannelReply reply, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> RetryDueAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ChannelStatusResponse GetStatus() => throw new NotSupportedException();
    }

    private static WebFormRequest Request(string email = "nour@example.com") =>
        new("Nour Ali", email, "Printer broken", "It prints blank pages.", "captcha-ok", null);

    [Fact]
    public async Task Submit_CreatesAWebFormTicket_ForANewCustomer()
    {
        var receipt = await Service().SubmitAsync(Request(), "10.0.0.1", CancellationToken.None);

        var (customerId, request, channel) = Assert.Single(_tickets.Created);
        Assert.Equal(TicketChannel.WebForm, channel);
        Assert.Equal(("Printer broken", "It prints blank pages."), (request.Subject, request.Description));
        Assert.Equal("TKT-000001", receipt.Number);
        var customer = Assert.Single(_customers.Created);
        Assert.Equal(("Nour Ali", "nour@example.com"), (customer.Name, customer.Email));
        Assert.Equal(_customers.Customers.Single().Id, customerId);
    }

    [Fact]
    public async Task Submit_ReusesTheCustomerWithTheSameEmail()
    {
        var existing = _customers.AddCustomer("Nour", email: "nour@example.com");

        await Service().SubmitAsync(Request(), "10.0.0.1", CancellationToken.None);

        Assert.Empty(_customers.Created);
        Assert.Equal(existing.Id, Assert.Single(_tickets.Created).CustomerId);
    }

    [Fact]
    public async Task Submit_BeyondTheLimit_ThrowsRateLimitExceeded_AndCreatesNothingMore()
    {
        var service = Service();
        await service.SubmitAsync(Request(), "10.0.0.1", CancellationToken.None);
        await service.SubmitAsync(Request("two@example.com"), "10.0.0.1", CancellationToken.None);

        var error = await Assert.ThrowsAsync<RateLimitExceededException>(
            () => service.SubmitAsync(Request("three@example.com"), "10.0.0.1", CancellationToken.None));

        Assert.True(error.RetryAfter > TimeSpan.Zero);
        Assert.Equal(2, _tickets.Created.Count);
        await service.SubmitAsync(Request("other@example.com"), "10.0.0.2", CancellationToken.None); // another IP is not limited
    }

    [Fact]
    public async Task Submit_WithAFailedCaptcha_ThrowsValidationOnCaptchaToken()
    {
        _captcha.Result = false;

        var error = await Assert.ThrowsAsync<ValidationException>(() => Service().SubmitAsync(Request(), "10.0.0.1", CancellationToken.None));

        Assert.Contains("captchaToken", error.Errors.Keys);
        Assert.Empty(_tickets.Created);
        Assert.Equal(("captcha-ok", "10.0.0.1"), Assert.Single(_captcha.Calls));
    }

    [Fact]
    public async Task Submit_WithMissingFields_ThrowsValidation_BeforeTheCaptchaIsChecked()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service().SubmitAsync(Request() with { Subject = null }, "10.0.0.1", CancellationToken.None));

        Assert.Contains("subject", error.Errors.Keys);
        Assert.Empty(_captcha.Calls);
    }

    [Fact]
    public async Task Submit_SendsAConfirmationEmail_WithTheTicketTag()
    {
        var receipt = await Service().SubmitAsync(Request(), "10.0.0.1", CancellationToken.None);

        var sent = Assert.Single(_sender.Sent);
        Assert.Equal(ChannelKind.Email, sent.Channel);
        Assert.Equal("nour@example.com", sent.Recipient);
        Assert.Contains("[TKT-000001]", sent.Subject);
        Assert.Contains(receipt.Number!, sent.Body);
    }

    [Fact]
    public async Task Submit_WhenTheMailFails_StillSucceeds()
    {
        var receipt = await Service(new ThrowingSender()).SubmitAsync(Request(), "10.0.0.1", CancellationToken.None);

        Assert.Equal("TKT-000001", receipt.Number);
    }

    [Fact]
    public async Task Submit_AsChatChannel_CreatesAChatTicket()
    {
        await Service().SubmitAsync(Request(), "10.0.0.1", CancellationToken.None, TicketChannel.Chat);

        Assert.Equal(TicketChannel.Chat, Assert.Single(_tickets.Created).Channel);
    }

    [Fact]
    public async Task Submit_WithTheHoneypotFilled_CreatesNothing()
    {
        var receipt = await Service().SubmitAsync(Request() with { Website = "http://spam.example" }, "10.0.0.1", CancellationToken.None);

        Assert.Null(receipt.Number);
        Assert.Empty(_tickets.Created);
        Assert.Empty(_sender.Sent);
    }
}
