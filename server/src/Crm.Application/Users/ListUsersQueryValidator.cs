using Crm.Application.Common.Paging;
using FluentValidation;

namespace Crm.Application.Users;

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithName(_ => UserText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => UserText.PageSizeField);
    }
}
