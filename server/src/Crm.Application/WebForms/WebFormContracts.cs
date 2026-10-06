using Crm.Application.Common.Localization;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using FluentValidation;

namespace Crm.Application.WebForms;

/// <summary>
/// What a visitor sends from an embedded web form. <c>CaptchaToken</c> comes from the captcha widget; <c>Website</c> is a
/// honeypot field real users never see (filled → bot).
/// </summary>
public sealed record WebFormRequest(string? Name, string? Email, string? Subject, string? Message, string? CaptchaToken, string? Website);

/// <summary>The ticket opened by a submission (<c>Number</c> "TKT-000001"; null when the submission was dropped as spam).</summary>
public sealed record WebFormReceipt(string? Number);

/// <summary>GET /api/public/web-forms/config: what the embedded form needs to render the captcha.</summary>
public sealed record WebFormConfigResponse(bool CaptchaRequired, string? CaptchaSiteKey);

/// <summary>Verifies the captcha answer with the provider (HTTP in Infrastructure; tests use a fake).</summary>
public interface ICaptchaVerifier
{
    /// <summary>True when the token is valid, or when no captcha is configured. Provider failures count as invalid.</summary>
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}

/// <summary>Name, email, subject and message are required and limited in length (errors name the field).</summary>
public sealed class WebFormRequestValidator : AbstractValidator<WebFormRequest>
{
    public WebFormRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Customer.NameMaxLength).WithName(_ => WebFormText.NameField);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(Customer.EmailMaxLength).EmailAddress().WithName(_ => WebFormText.EmailField);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength).WithName(_ => WebFormText.SubjectField);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(Ticket.DescriptionMaxLength).WithName(_ => WebFormText.MessageField);
    }
}

public static class WebFormText
{
    public static string NameField => LocalizedText.Get("Name", "الاسم");

    public static string EmailField => LocalizedText.Get("Email", "البريد الإلكتروني");

    public static string SubjectField => LocalizedText.Get("Subject", "الموضوع");

    public static string MessageField => LocalizedText.Get("Message", "الرسالة");

    public static string CaptchaFailed => LocalizedText.Get(
        "The captcha check failed. Try again.",
        "فشل التحقق من الكابتشا. حاول مرة أخرى.");

    public static string ConfirmationSubject => LocalizedText.Get("We received your request", "استلمنا طلبك");

    public static string ConfirmationBody(string number, string subject) => LocalizedText.Get(
        $"Thank you for contacting us.\nYour request \"{subject}\" was received as ticket {number}.\nReply to this email to add more information; keep the ticket number in the subject.",
        $"شكراً لتواصلك معنا.\nتم استلام طلبك \"{subject}\" برقم التذكرة {number}.\nيمكنك الرد على هذه الرسالة لإضافة معلومات؛ أبقِ رقم التذكرة في العنوان.");
}
