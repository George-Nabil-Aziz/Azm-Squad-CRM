using Crm.Application.Common.Paging;
using FluentValidation;

namespace Crm.Application.Customers.Timeline;

public sealed class CustomerTimelineQueryValidator : AbstractValidator<CustomerTimelineQuery>
{
    public CustomerTimelineQueryValidator()
    {
        RuleFor(x => x.Type).Must(type => InteractionTypes.TryParse(type, out _))
            .WithName(_ => CustomerText.TimelineTypeField).WithMessage(_ => CustomerText.TimelineTypeInvalid)
            .When(x => x.Type is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(PagingDefaults.DefaultPage).WithName(_ => PagingText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => PagingText.PageSizeField);
    }
}
