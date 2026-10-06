using Crm.Domain.Tickets;
using FluentValidation;

namespace Crm.Application.Tickets;

/// <summary>Create and edit rules: name required (max 100). Uniqueness is checked by the service.</summary>
public sealed class TicketCategoryRequestValidator : AbstractValidator<TicketCategoryRequest>
{
    public TicketCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(TicketCategory.NameMaxLength).WithName(_ => TicketCategoryText.NameField);
    }
}
