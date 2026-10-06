using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Add-contact rules: a known type; a real phone number for phone / WhatsApp, a valid address for email.</summary>
public sealed class CustomerContactRequestValidator : AbstractValidator<CustomerContactRequest>
{
    public CustomerContactRequestValidator()
    {
        RuleFor(x => x.Type).Must(type => ContactValues.TryParseType(type, out _))
            .WithName(_ => CustomerText.ContactTypeField).WithMessage(_ => CustomerText.ContactTypeInvalid);

        RuleFor(x => x.Value).NotEmpty().WithName(_ => CustomerText.ContactValueField);
        RuleFor(x => x.Value).MaximumLength(CustomerContact.ValueMaxLength).EmailAddress()
            .WithName(_ => CustomerText.ContactValueField)
            .When(x => IsType(x, ContactType.Email) && !string.IsNullOrWhiteSpace(x.Value));
        RuleFor(x => x.Value).Must(value => ContactValues.TryNormalizePhone(value, out _))
            .WithName(_ => CustomerText.ContactValueField).WithMessage(_ => CustomerText.PhoneInvalid)
            .When(x => (IsType(x, ContactType.Phone) || IsType(x, ContactType.WhatsApp)) && !string.IsNullOrWhiteSpace(x.Value));
    }

    private static bool IsType(CustomerContactRequest request, ContactType type) =>
        ContactValues.TryParseType(request.Type, out var parsed) && parsed == type;
}
