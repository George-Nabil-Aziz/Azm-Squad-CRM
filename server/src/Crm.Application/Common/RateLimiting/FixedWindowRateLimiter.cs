using System.Collections.Concurrent;

namespace Crm.Application.Common.RateLimiting;

/// <summary>Fixed window counter per key. Register as a singleton.</summary>
public sealed class FixedWindowRateLimiter(TimeProvider timeProvider) : IRateLimiter
{
    private const int PruneEvery = 256;

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private int _calls;

    public bool TryAcquire(string key, int limit, TimeSpan window, out TimeSpan retryAfter)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var now = timeProvider.GetUtcNow();
        if (Interlocked.Increment(ref _calls) % PruneEvery == 0)
        {
            Prune(now);
        }

        var entry = _windows.GetOrAdd(key, _ => new Window(now, window));
        lock (entry)
        {
            if (now >= entry.Start + entry.Length)
            {
                entry.Start = now;
                entry.Length = window;
                entry.Count = 0;
            }

            if (entry.Count < limit)
            {
                entry.Count++;
                retryAfter = TimeSpan.Zero;
                return true;
            }

            retryAfter = entry.Start + entry.Length - now;
            return false;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, entry) in _windows)
        {
            if (now >= entry.Start + entry.Length)
            {
                _windows.TryRemove(key, out _);
            }
        }
    }

    private sealed class Window(DateTimeOffset start, TimeSpan length)
    {
        public DateTimeOffset Start { get; set; } = start;

        public TimeSpan Length { get; set; } = length;

        public int Count { get; set; }
    }
}
