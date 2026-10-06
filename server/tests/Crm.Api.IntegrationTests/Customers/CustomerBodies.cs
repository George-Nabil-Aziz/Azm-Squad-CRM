namespace Crm.Api.IntegrationTests.Customers;

/// <summary>JSON shapes of /api/customers responses, as the client sees them.</summary>
public sealed record CustomerBody(
    Guid Id, string Name, string? Email, string? Phone, DateTime CreatedAt, DateTime UpdatedAt, ContactBody[] Contacts);

public sealed record ContactBody(Guid Id, string Type, string Value, bool IsPrimary);

public sealed record CustomerPageBody(CustomerBody[] Items, int Page, int PageSize, int TotalCount);

/// <summary>Test phone numbers.</summary>
public static class TestPhones
{
    /// <summary>A random valid Saudi mobile number in E.164 ("+96650" + 7 digits), so tests never share a number.</summary>
    public static string NewMobile() => $"+96650{Random.Shared.Next(1_000_000, 9_999_999)}";

    /// <summary>The same number as Saudis type it: "0" + the national number ("0501234567").</summary>
    public static string Local(string e164) => "0" + e164["+966".Length..];
}
