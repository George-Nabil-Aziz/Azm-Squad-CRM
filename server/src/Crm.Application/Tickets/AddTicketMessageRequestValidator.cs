using Crm.Domain.Tickets;
using FluentValidation;

namespace Crm.Application.Tickets;

/// <summary>A reply / note needs a body of at most <see cref="TicketMessage.BodyMaxLength"/> characters.</summary>
public sealed class AddTicketMessageRequestValidator : AbstractValidator<AddTicketMessageRequest>
{
    public AddTicketMessageRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(TicketMessage.BodyMaxLength).WithName(_ => TicketMessageText.BodyField);
        RuleFor(x => x.TemplateName).MaximumLength(100);
    }
}
