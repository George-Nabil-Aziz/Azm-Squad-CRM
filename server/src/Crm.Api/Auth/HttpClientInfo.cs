using System.Net;
using Crm.Application.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.Auth;

/// <summary>
/// The caller's address. <c>X-Forwarded-For</c> from a trusted proxy (loopback by default, e.g. the Vite dev proxy, or the
/// proxies in <c>ForwardedHeaders:KnownProxies</c>) is applied earlier by the ForwardedHeaders middleware, so this reads the
/// real client address from the connection.
/// </summary>
public sealed class HttpClientInfo(IHttpContextAccessor httpContextAccessor) : IClientInfo
{
    public string? IpAddress => ClientIp.Normalize(httpContextAccessor.HttpContext?.Connection.RemoteIpAddress);
}

public static class ClientIp
{
    /// <summary>Text of the address; an IPv4-mapped IPv6 address (::ffff:a.b.c.d) is shown as plain IPv4.</summary>
    public static string? Normalize(IPAddress? address) =>
        address is null ? null : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}

public static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Trusts X-Forwarded-For / -Proto only from loopback (default of the middleware) plus <c>ForwardedHeaders:KnownProxies</c>
    /// (IP addresses) and <c>ForwardedHeaders:KnownNetworks</c> (CIDR, e.g. 10.0.0.0/8). Anyone else cannot spoof their address.
    /// </summary>
    public static IServiceCollection AddCrmForwardedHeaders(this IServiceCollection services, IConfiguration configuration) =>
        services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
            {
                if (System.Net.IPNetwork.TryParse(network, out var parsed))
                {
                    options.KnownIPNetworks.Add(parsed);
                }
            }
        });
}
