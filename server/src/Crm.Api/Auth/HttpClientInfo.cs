using Crm.Application.Audit;

namespace Crm.Api.Auth;

/// <summary>The caller's address as the server sees it (no <c>X-Forwarded-For</c> trust; set up forwarded headers when deployed behind a proxy).</summary>
public sealed class HttpClientInfo(IHttpContextAccessor httpContextAccessor) : IClientInfo
{
    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
