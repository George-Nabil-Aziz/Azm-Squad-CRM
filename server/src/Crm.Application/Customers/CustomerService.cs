using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers;

/// <summary>Customer use cases: validation, the clock, the Domain rules, storage through <see cref="ICustomerRepository"/>.</summary>
public sealed class CustomerService(
    ICustomerRepository customers,
    TimeProvider timeProvider,
    IValidator<ListCustomersQuery> listValidator,
    IValidator<CustomerRequest> requestValidator,
    IValidator<CustomerContactRequest> contactValidator,
    IValidator<CustomerLookupQuery> lookupValidator,
    IInteractionRecorder timeline) : ICustomerService
{
    private static readonly ContactType[] NumberTypes = [ContactType.Phone, ContactType.WhatsApp];

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

        var now = UtcNow();
        var customer = Customer.Create(request.Name!, request.Email, PhoneOrNull(request.Phone), now);
        customers.Add(customer);
        timeline.Record(customer.Id, InteractionType.Customer, InteractionEvents.CustomerCreated, customer.Name, null, now);
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var customer = await FindAsync(id, cancellationToken);

        var now = UtcNow();
        customer.Update(request.Name!, request.Email, PhoneOrNull(request.Phone), now);
        timeline.Record(customer.Id, InteractionType.Customer, InteractionEvents.CustomerUpdated, customer.Name, null, now);
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(id, cancellationToken);

        customer.Delete(UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerContactResponse> AddContactAsync(
        Guid customerId, CustomerContactRequest request, CancellationToken cancellationToken)
    {
        await contactValidator.ValidateOrThrowAsync(request, cancellationToken);
        var customer = await FindAsync(customerId, cancellationToken);
        ContactValues.TryParseType(request.Type, out var type);
        var value = type == ContactType.Email ? request.Value! : PhoneOrNull(request.Value)!;
        if (customer.HasContact(type, value))
        {
            throw new ConflictException(CustomerText.ContactExists);
        }

        var now = UtcNow();
        var contact = customer.AddContact(type, value, request.IsPrimary == true, now);
        timeline.Record(customer.Id, InteractionType.Customer, InteractionEvents.ContactAdded, contact.Value, contact.Id, now);
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(contact);
    }

    public async Task MakeContactPrimaryAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken)
    {
        var customer = await FindWithContactAsync(customerId, contactId, cancellationToken);

        customer.MakeContactPrimary(contactId, UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveContactAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken)
    {
        var customer = await FindWithContactAsync(customerId, contactId, cancellationToken);

        customer.RemoveContact(contactId, UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerResponse>> LookupAsync(CustomerLookupQuery query, CancellationToken cancellationToken)
    {
        await lookupValidator.ValidateOrThrowAsync(query, cancellationToken);

        var found = string.IsNullOrWhiteSpace(query.Phone)
            ? await customers.FindByContactAsync(
                [ContactType.Email], CustomerContact.Normalize(ContactType.Email, query.Email!), cancellationToken)
            : await customers.FindByContactAsync(NumberTypes, PhoneOrNull(query.Phone)!, cancellationToken);

        return [.. found.Select(ToResponse)];
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>E.164 form of a phone number the validator accepted; null for an empty value.</summary>
    private static string? PhoneOrNull(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null
        : ContactValues.TryNormalizePhone(phone, out var e164) ? e164
        : throw new InvalidOperationException("The phone number was not validated.");

    private async Task<Customer> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await customers.FindAsync(id, cancellationToken) ?? throw new NotFoundException(CustomerText.NotFound);

    private async Task<Customer> FindWithContactAsync(Guid customerId, Guid contactId, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(customerId, cancellationToken);
        return customer.Contacts.Any(c => c.Id == contactId)
            ? customer
            : throw new NotFoundException(CustomerText.ContactNotFound);
    }

    private static CustomerResponse ToResponse(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.Phone, customer.CreatedAt, customer.UpdatedAt,
            [.. customer.Contacts
                .OrderBy(c => c.Type).ThenByDescending(c => c.IsPrimary).ThenBy(c => c.CreatedAt).ThenBy(c => c.Value)
                .Select(ToResponse)]);

    private static CustomerContactResponse ToResponse(CustomerContact contact) =>
        new(contact.Id, ContactValues.TypeName(contact.Type), contact.Value, contact.IsPrimary);
}
