namespace Crm.Application.Common.RateLimiting;

/// <summary>
/// Counts calls per key in a fixed time window (web forms per IP, API keys per key, chat starts per IP). In memory: a limit
/// is per server instance. The clock is the injected <see cref="TimeProvider"/>.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// True and counts the call when the key has made fewer than <paramref name="limit"/> calls in the current window;
    /// otherwise false with the time left until the window ends in <paramref name="retryAfter"/>.
    /// </summary>
    bool TryAcquire(string key, int limit, TimeSpan window, out TimeSpan retryAfter);
}

/// <summary>Too many calls: mapped to 429 ProblemDetails with a <c>Retry-After</c> header.</summary>
public sealed class RateLimitExceededException(TimeSpan retryAfter) : Exception("Too many requests.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
