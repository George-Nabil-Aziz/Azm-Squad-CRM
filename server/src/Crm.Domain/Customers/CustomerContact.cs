using System.Text.RegularExpressions;

namespace Crm.Domain.Customers;

/// <summary>
/// One phone number, email address or WhatsApp number of a customer. Part of the <see cref="Customer"/> aggregate:
/// added, made primary and removed only through <see cref="Customer"/> methods, which keep exactly one primary
/// contact per type (as long as the customer has a contact of that type).
/// </summary>
public sealed partial class CustomerContact
{
    /// <summary>Longest stored value (an email address; E.164 numbers have at most 16 characters).</summary>
    public const int ValueMaxLength = 256;

    private CustomerContact()
    {
        // EF Core materializes contacts through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public ContactType Type { get; private set; }

    /// <summary>Phone / WhatsApp: E.164 ("+966501234567"). Email: trimmed, lower case.</summary>
    public string Value { get; private set; } = string.Empty;

    public bool IsPrimary { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// The stored form of a value: emails are trimmed and lower-cased; phone and WhatsApp numbers must already be in
    /// E.164 format (the Application layer converts what people type) and are only trimmed. Throws
    /// <see cref="ArgumentException"/> for anything else.
    /// </summary>
    public static string Normalize(ContactType type, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        switch (type)
        {
            case ContactType.Email:
                if (trimmed.Length > ValueMaxLength || !trimmed.Contains('@'))
                {
                    throw new ArgumentException("The value is not an email address.", nameof(value));
                }

                return trimmed.ToLowerInvariant();
            case ContactType.Phone:
            case ContactType.WhatsApp:
                if (!E164().IsMatch(trimmed))
                {
                    throw new ArgumentException("Phone numbers must be in E.164 format, e.g. +966501234567.", nameof(value));
                }

                return trimmed;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown contact type.");
        }
    }

    internal static CustomerContact Create(Guid customerId, ContactType type, string normalizedValue, DateTime utcNow) =>
        new() { Id = Guid.NewGuid(), CustomerId = customerId, Type = type, Value = normalizedValue, CreatedAt = utcNow };

    internal void ChangeValue(string normalizedValue) => Value = normalizedValue;

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    /// <summary>"+" and 7 to 15 digits, the first not 0 (ITU-T E.164).</summary>
    [GeneratedRegex(@"^\+[1-9][0-9]{6,14}$")]
    private static partial Regex E164();
}
