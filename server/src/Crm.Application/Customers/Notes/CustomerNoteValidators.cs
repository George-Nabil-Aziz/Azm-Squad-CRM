using Crm.Application.Common.Paging;
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers.Notes;

public sealed class CustomerNoteRequestValidator : AbstractValidator<CustomerNoteRequest>
{
    public CustomerNoteRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(CustomerNote.TextMaxLength).WithName(_ => CustomerText.NoteTextField);
    }
}

public sealed class ListCustomerNotesQueryValidator : AbstractValidator<ListCustomerNotesQuery>
{
    public ListCustomerNotesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(PagingDefaults.DefaultPage).WithName(_ => PagingText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => PagingText.PageSizeField);
    }
}
