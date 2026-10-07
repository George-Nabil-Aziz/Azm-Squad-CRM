using Crm.Application.Audit;
using Crm.Application.Branches;
using Crm.Application.Common.Security;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Audit;
using Crm.Domain.Customers;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Customers;

/// <summary>Customer use cases: validation, the clock, the Domain rules, storage through <see cref="ICustomerRepository"/>.</summary>
public sealed class CustomerService(
    ICustomerRepository customers,
    TimeProvider timeProvider,
    IValidator<ListCustomersQuery> listValidator,
    IValidator<CustomerRequest> requestValidator,
    IValidator<CustomerContactRequest> contactValidator,
    IValidator<CustomerLookupQuery> lookupValidator,
    IInteractionRecorder timeline,
    IAuditLogger audit,
    IBranchRepository? branches = null,
    IDataScope? dataScope = null) : ICustomerService
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

        var branchId = await ResolveBranchAsync(request.BranchId, null, cancellationToken);
        var now = UtcNow();
        var customer = Customer.Create(request.Name!, request.Email, PhoneOrNull(request.Phone), now);
        customer.ChangeBranch(branchId, now);
        customers.Add(customer);
        timeline.Record(customer.Id, InteractionType.Customer, InteractionEvents.CustomerCreated, customer.Name, null, now);
        await customers.SaveChangesAsync(cancellationToken);

        return ToResponse(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var customer = await FindAsync(id, cancellationToken);
        var branchId = await ResolveBranchAsync(request.BranchId, customer.BranchId, cancellationToken);

        var now = UtcNow();
        customer.Update(request.Name!, request.Email, PhoneOrNull(request.Phone), now);
        var branchChanged = customer.ChangeBranch(branchId, now);
        timeline.Record(customer.Id, InteractionType.Customer, InteractionEvents.CustomerUpdated, customer.Name, null, now);
        await customers.SaveChangesAsync(cancellationToken);
        if (branchChanged)
        {
            await customers.MoveTicketsToBranchAsync(customer.Id, branchId, cancellationToken); // CRM-62
        }

        return ToResponse(customer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(id, cancellationToken);

        customer.Delete(UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(
            new AuditEvent(AuditActions.CustomerDeleted, "Customer", customer.Id.ToString(),
                new { customer.Name, customer.Email, customer.Phone }),
            cancellationToken);
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

        var removed = customer.Contacts.Single(c => c.Id == contactId);
        var oldValues = new { customerId = customer.Id, type = ContactValues.TypeName(removed.Type), removed.Value };
        customer.RemoveContact(contactId, UtcNow());
        await customers.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(
            new AuditEvent(AuditActions.CustomerContactRemoved, "CustomerContact", contactId.ToString(), oldValues),
            cancellationToken);
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

    /// <summary>
    /// CRM-62: the branch a customer is saved with. A branch-restricted user always works inside their own branch (naming another one
    /// is 400); anyone else may name an active branch. Not named: the current branch stays (create: none, or the user's own).
    /// </summary>
    private async Task<Guid?> ResolveBranchAsync(Guid? requested, Guid? current, CancellationToken cancellationToken)
    {
        if (dataScope is { RestrictBranch: true, BranchId: { } own })
        {
            if (requested is { } other && other != own)
            {
                throw new ValidationException(new Dictionary<string, string[]> { ["branchId"] = [BranchText.NotYours] });
            }

            return current ?? own;
        }

        if (requested is not { } id)
        {
            return current;
        }

        if (id == current)
        {
            return current;
        }

        if (branches is null || await branches.FindAsync(id, cancellationToken) is not { IsActive: true })
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["branchId"] = [BranchText.Unavailable] });
        }

        return id;
    }

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

    /// <summary>The API shape of a customer (also used by the ticket customer panel).</summary>
    public static CustomerResponse ToResponse(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.Phone, customer.CreatedAt, customer.UpdatedAt,
            [.. customer.Contacts
                .OrderBy(c => c.Type).ThenByDescending(c => c.IsPrimary).ThenBy(c => c.CreatedAt).ThenBy(c => c.Value)
                .Select(ToResponse)],
            customer.ErpCustomerId,
            customer.BranchId);

    private static CustomerContactResponse ToResponse(CustomerContact contact) =>
        new(contact.Id, ContactValues.TypeName(contact.Type), contact.Value, contact.IsPrimary);
}
