namespace Crm.Application.Portal;

/// <summary>The role of a signed-in portal customer (token claim <c>role</c>). It has no staff permission.</summary>
public static class PortalRoles
{
    public const string Customer = "Customer";
}

/// <summary>Body of POST /api/portal/auth/request-code.</summary>
public sealed record RequestCodeRequest(string? Email);

/// <summary>Body of POST /api/portal/auth/verify: the email and the 6-digit code that was mailed to it.</summary>
public sealed record VerifyCodeRequest(string? Email, string? Code);

/// <summary>The signed-in customer of the portal.</summary>
public sealed record PortalCustomerResponse(Guid Id, string Name, string Email);

/// <summary>Result of a successful sign-in: a bearer token (role <c>Customer</c>) and the customer.</summary>
public sealed record PortalLoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, PortalCustomerResponse Customer);
