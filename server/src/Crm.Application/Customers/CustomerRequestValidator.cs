using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Create and edit rules: name required; email and phone optional but well-formed when given.</summary>
public sealed class CustomerRequestValidator : AbstractValidator<CustomerRequest>
{
    public CustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Customer.NameMaxLength).WithName(_ => CustomerText.NameField);
        RuleFor(x => x.Email).MaximumLength(Customer.EmailMaxLength).EmailAddress().WithName(_ => CustomerText.EmailField)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        // A real phone number in any common format; the service stores it as E.164.
        RuleFor(x => x.Phone).MaximumLength(Customer.PhoneMaxLength).WithName(_ => CustomerText.PhoneField)
            .Must(phone => ContactValues.TryNormalizePhone(phone, out _)).WithMessage(_ => CustomerText.PhoneInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
