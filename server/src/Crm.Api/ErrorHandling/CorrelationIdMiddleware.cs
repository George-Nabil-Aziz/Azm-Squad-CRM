using System.Text.RegularExpressions;

namespace Crm.Api.ErrorHandling;

/// <summary>
/// Reads <c>X-Correlation-Id</c> from the request (or generates one), echoes it in the response header,
/// stores it in <see cref="HttpContext.Items"/> and opens a logging scope so every log line of the request carries it.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadValidHeader(context) ?? Guid.NewGuid().ToString("N");
        context.Items[ItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await next(context);
        }
    }

    /// <summary>Correlation id of the current request; falls back to <see cref="HttpContext.TraceIdentifier"/>.</summary>
    public static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id ? id : context.TraceIdentifier;

    private static string? ReadValidHeader(HttpContext context)
    {
        var value = context.Request.Headers[HeaderName].ToString();
        return AllowedFormat().IsMatch(value) ? value : null;
    }

    // Untrusted input: only short, log-safe ids are accepted; anything else is replaced by a new id.
    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex AllowedFormat();
}
