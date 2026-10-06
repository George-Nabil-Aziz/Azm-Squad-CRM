using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Customer use cases: validation, the clock, the Domain rules, storage through <see cref="ICustomerRepository"/>.</summary>
public sealed class CustomerService(
    ICustomerRepository customers,
    TimeProvider timeProvider,
    IValidator<ListCustomersQuery> listValidator,
    IValidator<CustomerRequest> requestValidator) : ICustomerService
{
    public async Task<PagedResult<CustomerResponse>> ListAsync(ListCustomersQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        var search = query.Search?.Trim();

        var page = await customers.ListAsync(
            string.IsNullOrEmpty(search) ? null : search,
            query.Page ?? PagingDefaults.DefaultPage,
            query.PageSize ?? PagingDefaults.DefaultPageSize,
            cancellationToken);

        return new PagedResult<CustomerResponse>([.. page.Items.Select(ToResponse)], page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<CustomerResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await FindAsync(id, cancellationToken));

    public async Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);

        var customer = Customer.Create(request.Name!, request.Email, request.Phone, UtcNow());
        customers.Add(customer);
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var customer = await FindAsync(id, cancellationToken);

        customer.Update(request.Name!, request.Email, request.Phone, UtcNow());
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(id, cancellationToken);

        customer.Delete(UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private async Task<Customer> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await customers.FindAsync(id, cancellationToken) ?? throw new NotFoundException(CustomerText.NotFound);

    private static CustomerResponse ToResponse(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.Phone, customer.CreatedAt, customer.UpdatedAt);
}
