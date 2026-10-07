using Crm.Domain.Common;

namespace Crm.Domain.Customers;

/// <summary>
/// A customer that tickets belong to, with any number of phone numbers, email addresses and WhatsApp numbers
/// (<see cref="Contacts"/>). Every contact type the customer has keeps exactly one primary contact;
/// <see cref="Email"/> and <see cref="Phone"/> are copies of the primary email / phone (kept in sync here) so lists and
/// searches need no join. Times are UTC and come from the caller (the Application layer passes the injected
/// TimeProvider's time), so the rules are testable without a clock or a database.
/// </summary>
public sealed class Customer : ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 32;
    public const int ErpCustomerIdMaxLength = 100;

    private readonly List<CustomerContact> _contacts = [];

    private Customer()
    {
        // EF Core materializes customers through this constructor.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The primary email contact's value (null when the customer has no email).</summary>
    public string? Email { get; private set; }

    /// <summary>The primary phone contact's value, E.164 (null when the customer has no phone).</summary>
    public string? Phone { get; private set; }

    /// <summary>The id of the same customer in the ERP (CRM-60); null = not linked. Unique among customers.</summary>
    public string? ErpCustomerId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>The branch that manages the customer (CRM-62); null = no branch.</summary>
    public Guid? BranchId { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public IReadOnlyList<CustomerContact> Contacts => _contacts;

    /// <summary>
    /// A new customer. Name is required and trimmed; a given email / phone (E.164) becomes the primary contact of
    /// that type; empty values add nothing.
    /// </summary>
    public static Customer Create(string name, string? email, string? phone, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        var customer = new Customer { Id = Guid.NewGuid(), CreatedAt = utcNow };
        customer.SetProfile(name, email, phone, utcNow);
        return customer;
    }

    /// <summary>
    /// Replaces the profile. Email / phone set the primary contact of that type: a value the customer already has
    /// becomes primary, a new value replaces the primary's value, an empty value removes the primary contact (the
    /// oldest other contact of that type becomes primary). Other contacts stay. A deleted customer cannot be changed.
    /// </summary>
    public void Update(string name, string? email, string? phone, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        SetProfile(name, email, phone, utcNow);
    }

    /// <summary>Links the customer to its ERP customer (trimmed); null or blank removes the link.</summary>
    public void LinkErp(string? erpCustomerId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        var id = erpCustomerId?.Trim();
        if (id is { Length: > ErpCustomerIdMaxLength })
        {
            throw new ArgumentException("The ERP customer id is too long.", nameof(erpCustomerId));
        }

        ErpCustomerId = string.IsNullOrEmpty(id) ? null : id;
        UpdatedAt = utcNow;
    }

    /// <summary>Soft delete: the row and its data stay (tickets keep pointing at it). Deleting twice changes nothing.</summary>
    /// <summary>Moves the customer to a branch (null = none). Returns false, changing nothing, when it already is there.</summary>
    public bool ChangeBranch(Guid? branchId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (BranchId == branchId)
        {
            return false;
        }

        BranchId = branchId;
        UpdatedAt = utcNow;
        return true;
    }

    public void Delete(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>True when the customer already has this value (compared in its stored form) for the type.</summary>
    public bool HasContact(ContactType type, string value) =>
        FindContact(type, CustomerContact.Normalize(type, value)) is not null;

    /// <summary>
    /// Adds a contact (phone / WhatsApp values must be E.164). It becomes primary when <paramref name="isPrimary"/> is
    /// true (the old primary of that type is unset) or when it is the first contact of its type. Throws
    /// <see cref="InvalidOperationException"/> when the customer already has the value.
    /// </summary>
    public CustomerContact AddContact(ContactType type, string value, bool isPrimary, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        var normalized = CustomerContact.Normalize(type, value);
        if (FindContact(type, normalized) is not null)
        {
            throw new InvalidOperationException("The customer already has this contact.");
        }

        var contact = CustomerContact.Create(Id, type, normalized, utcNow);
        _contacts.Add(contact);
        if (isPrimary || PrimaryContact(type) is null)
        {
            MakePrimary(contact);
        }

        UpdatedAt = utcNow;
        return contact;
    }

    /// <summary>Makes the contact the primary one of its type; the old primary of that type is unset.</summary>
    public void MakeContactPrimary(Guid contactId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        MakePrimary(GetContact(contactId));
        UpdatedAt = utcNow;
    }

    /// <summary>Removes the contact. When it was primary, the oldest other contact of that type becomes primary.</summary>
    public void RemoveContact(Guid contactId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureNotDeleted();
        Remove(GetContact(contactId));
        UpdatedAt = utcNow;
    }

    private void SetProfile(string name, string? email, string? phone, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        SetPrimaryValue(ContactType.Email, email, utcNow);
        SetPrimaryValue(ContactType.Phone, phone, utcNow);
        UpdatedAt = utcNow;
    }

    private void SetPrimaryValue(ContactType type, string? value, DateTime utcNow)
    {
        var primary = PrimaryContact(type);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (primary is not null)
            {
                Remove(primary);
            }

            return;
        }

        var normalized = CustomerContact.Normalize(type, value);
        var existing = FindContact(type, normalized);
        if (existing is not null)
        {
            MakePrimary(existing);
        }
        else if (primary is not null)
        {
            primary.ChangeValue(normalized);
            SyncPrimaryValues();
        }
        else
        {
            var contact = CustomerContact.Create(Id, type, normalized, utcNow);
            _contacts.Add(contact);
            MakePrimary(contact);
        }
    }

    private void MakePrimary(CustomerContact contact)
    {
        foreach (var other in _contacts.Where(c => c.Type == contact.Type))
        {
            other.SetPrimary(other == contact);
        }

        SyncPrimaryValues();
    }

    private void Remove(CustomerContact contact)
    {
        _contacts.Remove(contact);
        var next = contact.IsPrimary
            ? _contacts.Where(c => c.Type == contact.Type).OrderBy(c => c.CreatedAt).FirstOrDefault()
            : null;
        if (next is not null)
        {
            MakePrimary(next);
        }
        else
        {
            SyncPrimaryValues();
        }
    }

    private void SyncPrimaryValues()
    {
        Email = PrimaryContact(ContactType.Email)?.Value;
        Phone = PrimaryContact(ContactType.Phone)?.Value;
    }

    private CustomerContact? PrimaryContact(ContactType type) =>
        _contacts.FirstOrDefault(c => c.Type == type && c.IsPrimary);

    private CustomerContact? FindContact(ContactType type, string normalizedValue) =>
        _contacts.FirstOrDefault(c => c.Type == type && c.Value == normalizedValue);

    private CustomerContact GetContact(Guid contactId) =>
        _contacts.FirstOrDefault(c => c.Id == contactId)
        ?? throw new ArgumentException("The customer has no contact with this id.", nameof(contactId));

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted customer cannot be changed.");
        }
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
