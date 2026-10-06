using Crm.Application.Common.Paging;
using FluentValidation;

namespace Crm.Application.Tickets;

/// <summary>Ticket list rules: known status / priority names, a date range that does not end before it starts, paging.</summary>
public sealed class ListTicketsQueryValidator : AbstractValidator<ListTicketsQuery>
{
    public ListTicketsQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => TicketValues.TryParseStatus(status, out _)).WithMessage(_ => TicketText.StatusInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
        RuleFor(x => x.Priority)
            .Must(priority => TicketValues.TryParsePriority(priority, out _)).WithMessage(_ => TicketText.PriorityInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.Priority));
        RuleFor(x => x.CreatedTo)
            .Must((query, to) => to >= query.CreatedFrom).WithMessage(_ => TicketText.DateRangeInvalid)
            .When(x => x.CreatedFrom is not null && x.CreatedTo is not null);
        RuleFor(x => x.AssigneeId)
            .Null().WithMessage(_ => TicketText.AssigneeOrUnassigned)
            .When(x => x.Unassigned == true);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(PagingDefaults.DefaultPage).WithName(_ => PagingText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => PagingText.PageSizeField);
    }
}
