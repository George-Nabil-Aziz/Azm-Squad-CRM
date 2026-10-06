using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Lookup rules: exactly one of phone (a real number) or email (a valid address).</summary>
public sealed class CustomerLookupQueryValidator : AbstractValidator<CustomerLookupQuery>
{
    public CustomerLookupQueryValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().WithMessage(_ => CustomerText.LookupNeedsPhoneOrEmail)
            .When(x => string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).Must(phone => ContactValues.TryNormalizePhone(phone, out _))
            .WithMessage(_ => CustomerText.PhoneInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email).Empty().WithMessage(_ => CustomerText.LookupPhoneOrEmailOnly)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email).EmailAddress().WithName(_ => CustomerText.EmailField)
            .When(x => string.IsNullOrWhiteSpace(x.Phone) && !string.IsNullOrWhiteSpace(x.Email));
    }
}
