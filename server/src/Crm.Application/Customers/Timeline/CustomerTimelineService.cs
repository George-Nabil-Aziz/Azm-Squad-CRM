using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers.Timeline;

public sealed class CustomerTimelineService(
    ICustomerRepository customers,
    ICustomerTimelineRepository timeline,
    IValidator<CustomerTimelineQuery> validator) : ICustomerTimelineService
{
    public async Task<PagedResult<CustomerInteractionResponse>> ListAsync(
        Guid customerId, CustomerTimelineQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);
        _ = await customers.FindAsync(customerId, cancellationToken) ?? throw new NotFoundException(CustomerText.NotFound);

        InteractionType? type = InteractionTypes.TryParse(query.Type, out var parsed) ? parsed : null;
        return await timeline.ListAsync(
            customerId,
            type,
            query.Page ?? PagingDefaults.DefaultPage,
            query.PageSize ?? PagingDefaults.DefaultPageSize,
            cancellationToken);
    }
}
