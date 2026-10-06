using System.Text.RegularExpressions;
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Create and edit rules: name required; email and phone optional but well-formed when given.</summary>
public sealed partial class CustomerRequestValidator : AbstractValidator<CustomerRequest>
{
    private const int MinPhoneDigits = 6;

    public CustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Customer.NameMaxLength).WithName(_ => CustomerText.NameField);
        RuleFor(x => x.Email).MaximumLength(Customer.EmailMaxLength).EmailAddress().WithName(_ => CustomerText.EmailField)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        // Loose format for now: CRM-9 normalizes phone numbers to E.164.
        RuleFor(x => x.Phone).MaximumLength(Customer.PhoneMaxLength).WithName(_ => CustomerText.PhoneField)
            .Must(BeAPhoneNumber).WithMessage(_ => CustomerText.PhoneInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }

    private static bool BeAPhoneNumber(string? phone)
    {
        var value = phone!.Trim();
        return PhoneCharacters().IsMatch(value) && value.Count(char.IsAsciiDigit) >= MinPhoneDigits;
    }

    /// <summary>Optional leading +, then ASCII digits, spaces, dashes and brackets only.</summary>
    [GeneratedRegex(@"^\+?[0-9 ()\-]+$")]
    private static partial Regex PhoneCharacters();
}
