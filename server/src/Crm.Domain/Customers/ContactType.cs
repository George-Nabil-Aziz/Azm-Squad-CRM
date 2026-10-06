namespace Crm.Domain.Customers;

/// <summary>Kind of a <see cref="CustomerContact"/>. Stored by name ("Phone", "Email", "WhatsApp").</summary>
public enum ContactType
{
    /// <summary>A phone number in E.164 format ("+966501234567").</summary>
    Phone = 1,

    /// <summary>An email address, stored trimmed and in lower case.</summary>
    Email = 2,

    /// <summary>A WhatsApp number in E.164 format.</summary>
    WhatsApp = 3,
}
