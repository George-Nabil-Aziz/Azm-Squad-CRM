using Crm.Domain.Tickets;
using FluentValidation;

namespace Crm.Application.Tickets;

/// <summary>Create rules: customer and subject required; priority (when given) high / mid / low. Existence is checked by the service.</summary>
public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithName(_ => TicketText.CustomerField);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength).WithName(_ => TicketText.SubjectField);
        RuleFor(x => x.Description).MaximumLength(Ticket.DescriptionMaxLength).WithName(_ => TicketText.DescriptionField);
        RuleFor(x => x.Priority)
            .Must(priority => TicketValues.TryParsePriority(priority, out _)).WithMessage(_ => TicketText.PriorityInvalid)
            .WithName(_ => TicketText.PriorityField)
            .When(x => !string.IsNullOrWhiteSpace(x.Priority));
    }
}
