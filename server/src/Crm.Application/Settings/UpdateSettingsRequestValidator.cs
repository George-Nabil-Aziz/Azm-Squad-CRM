using System.Globalization;
using Crm.Domain.Tickets;
using FluentValidation;

namespace Crm.Application.Settings;

public sealed class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    private static readonly string[] SecurityModes = ["None", "Auto", "StartTls", "SslOnConnect"];

    public UpdateSettingsRequestValidator()
    {
        When(x => x.BusinessHours is not null, () =>
        {
            RuleFor(x => x.BusinessHours!.Enabled).NotNull().WithMessage(_ => SettingsText.EnabledRequired);
            RuleFor(x => x.BusinessHours!.Days)
                .Must(days => days is not null && days.All(day => SettingsSnapshot.DayNames.Contains(day, StringComparer.Ordinal)))
                .WithMessage(_ => SettingsText.DaysInvalid);
            RuleFor(x => x.BusinessHours!.Days)
                .Must(days => days is { Count: > 0 })
                .When(x => x.BusinessHours!.Enabled == true)
                .WithMessage(_ => SettingsText.DaysRequired);
            RuleFor(x => x.BusinessHours!.Start).Must(IsTime).WithMessage(_ => SettingsText.TimeInvalid);
            RuleFor(x => x.BusinessHours!.End).Must(IsTime).WithMessage(_ => SettingsText.TimeInvalid);
            RuleFor(x => x.BusinessHours!.End)
                .Must((hours, end) => IsTime(hours.BusinessHours!.Start) && IsTime(end)
                                      && Parse(hours.BusinessHours.Start!) < Parse(end!))
                .When(x => IsTime(x.BusinessHours!.Start) && IsTime(x.BusinessHours.End))
                .WithMessage(_ => SettingsText.EndBeforeStart);
        });

        RuleFor(x => x.TimeZone)
            .Must(zone => TimeZoneInfo.TryFindSystemTimeZoneById(zone!.Trim(), out _))
            .When(x => x.TimeZone is not null)
            .WithMessage(_ => SettingsText.TimeZoneInvalid);

        RuleFor(x => x.TicketPrefix)
            .Must(prefix => Ticket.IsValidPrefix(prefix?.Trim()))
            .When(x => x.TicketPrefix is not null)
            .WithMessage(_ => SettingsText.PrefixInvalid);

        When(x => x.Email is not null, () =>
        {
            RuleFor(x => x.Email!.FromAddress).EmailAddress().MaximumLength(256)
                .When(x => !string.IsNullOrWhiteSpace(x.Email!.FromAddress)).WithMessage(_ => SettingsText.EmailInvalid);
            RuleFor(x => x.Email!.SmtpPort).InclusiveBetween(1, 65535).When(x => x.Email!.SmtpPort is not null)
                .WithMessage(_ => SettingsText.PortInvalid);
            RuleFor(x => x.Email!.ImapPort).InclusiveBetween(1, 65535).When(x => x.Email!.ImapPort is not null)
                .WithMessage(_ => SettingsText.PortInvalid);
            RuleFor(x => x.Email!.SmtpSecurity).Must(IsSecurityMode).When(x => !string.IsNullOrWhiteSpace(x.Email!.SmtpSecurity))
                .WithMessage(_ => SettingsText.SecurityInvalid);
            RuleFor(x => x.Email!.ImapSecurity).Must(IsSecurityMode).When(x => !string.IsNullOrWhiteSpace(x.Email!.ImapSecurity))
                .WithMessage(_ => SettingsText.SecurityInvalid);
            RuleFor(x => x.Email!.SmtpHost).MaximumLength(256);
            RuleFor(x => x.Email!.ImapHost).MaximumLength(256);
            RuleFor(x => x.Email!.FromName).MaximumLength(200);
            RuleFor(x => x.Email!.SmtpUserName).MaximumLength(256);
            RuleFor(x => x.Email!.ImapUserName).MaximumLength(256);
            RuleFor(x => x.Email!.ImapFolder).MaximumLength(200);
        });

        RuleFor(x => x.WhatsApp!.PhoneNumberId).MaximumLength(64).When(x => x.WhatsApp is not null);

        When(x => x.Secrets is not null, () =>
        {
            RuleFor(x => x.Secrets!.SmtpPassword).MaximumLength(512);
            RuleFor(x => x.Secrets!.ImapPassword).MaximumLength(512);
            RuleFor(x => x.Secrets!.WhatsAppAccessToken).MaximumLength(2048);
            RuleFor(x => x.Secrets!.WhatsAppAppSecret).MaximumLength(512);
            RuleFor(x => x.Secrets!.WhatsAppVerifyToken).MaximumLength(512);
        });
    }

    private static bool IsSecurityMode(string? mode) => SecurityModes.Contains(mode?.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool IsTime(string? text) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static TimeOnly Parse(string text) =>
        TimeOnly.ParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None);
}
