using Crm.Application.Channels;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.RateLimiting;
using Crm.Application.Common.Validation;
using Crm.Application.Customers;
using Crm.Application.Tickets;
using Crm.Domain.Channels;
using Crm.Domain.Tickets;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.WebForms;

/// <summary>
/// Tickets from the embedded web forms (CRM-55). Order of checks: rate limit (429), fields (400), captcha (400 on
/// <c>captchaToken</c>). The customer is matched by email or created.
/// </summary>
public interface IWebFormService
{
    WebFormConfigResponse GetConfig();

    Task<WebFormReceipt> SubmitAsync(WebFormRequest request, string? remoteIp, CancellationToken cancellationToken);
}

public sealed class WebFormService(
    WebFormOptions options,
    IRateLimiter rateLimiter,
    ICaptchaVerifier captcha,
    IValidator<WebFormRequest> validator,
    ICustomerService customers,
    ITicketService tickets,
    IChannelSender sender) : IWebFormService
{
    public WebFormConfigResponse GetConfig() => new(options.CaptchaConfigured, options.CaptchaConfigured ? options.CaptchaSiteKey : null);

    public async Task<WebFormReceipt> SubmitAsync(WebFormRequest request, string? remoteIp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!rateLimiter.TryAcquire(
                $"webform:{remoteIp ?? "unknown"}", Math.Max(1, options.RateLimitRequests),
                TimeSpan.FromSeconds(Math.Max(1, options.RateLimitWindowSeconds)), out var retryAfter))
        {
            throw new RateLimitExceededException(retryAfter);
        }

        await validator.ValidateOrThrowAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return new WebFormReceipt(null); // honeypot: a bot gets a normal answer and nothing is stored
        }

        if (!await captcha.VerifyAsync(request.CaptchaToken, remoteIp, cancellationToken))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["captchaToken"] = [WebFormText.CaptchaFailed] });
        }

        var email = request.Email!.Trim();
        var customer = (await customers.LookupAsync(new CustomerLookupQuery(null, email), cancellationToken)).FirstOrDefault()
                       ?? await customers.CreateAsync(new CustomerRequest(request.Name!.Trim(), email, null), cancellationToken);
        var ticket = await tickets.CreateForCustomerAsync(
            customer.Id,
            new CreateTicketRequest(customer.Id, request.Subject!.Trim(), request.Message!.Trim(), null, null),
            TicketChannel.WebForm,
            cancellationToken);

        await SendConfirmationAsync(email, ticket, cancellationToken);
        return new WebFormReceipt(ticket.Number);
    }

    /// <summary>The "[TKT-n]" tag lets the visitor's email reply land on the ticket. A mail failure never fails the submit.</summary>
    private async Task SendConfirmationAsync(string email, TicketResponse ticket, CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(
                new ChannelReply(
                    ChannelKind.Email, email,
                    TicketNumberTag.AppendTo(WebFormText.ConfirmationSubject, TicketDisplayNumber.Sequence(ticket.Number)),
                    WebFormText.ConfirmationBody(ticket.Number, ticket.Subject), null, ticket.Id),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The ticket exists; the visitor just gets no confirmation.
        }
    }
}
