using Crm.Application.Common.RateLimiting;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Common;

public class FixedWindowRateLimiterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 6, 8, 0, 0, TimeSpan.Zero));

    private FixedWindowRateLimiter Limiter() => new(_clock);

    [Fact]
    public void AllowsTheLimit_ThenRefusesWithARetryAfter()
    {
        var limiter = Limiter();

        Assert.True(limiter.TryAcquire("ip-1", 2, Window, out _));
        Assert.True(limiter.TryAcquire("ip-1", 2, Window, out _));
        Assert.False(limiter.TryAcquire("ip-1", 2, Window, out var retryAfter));
        Assert.Equal(Window, retryAfter);
    }

    [Fact]
    public void RetryAfter_ShrinksAsTheWindowPasses()
    {
        var limiter = Limiter();
        limiter.TryAcquire("k", 1, Window, out _);
        _clock.UtcNow += TimeSpan.FromSeconds(45);

        Assert.False(limiter.TryAcquire("k", 1, Window, out var retryAfter));
        Assert.Equal(TimeSpan.FromSeconds(15), retryAfter);
    }

    [Fact]
    public void ANewWindow_AllowsAgain()
    {
        var limiter = Limiter();
        limiter.TryAcquire("k", 1, Window, out _);
        Assert.False(limiter.TryAcquire("k", 1, Window, out _));

        _clock.UtcNow += Window;

        Assert.True(limiter.TryAcquire("k", 1, Window, out _));
    }

    [Fact]
    public void KeysAreIndependent()
    {
        var limiter = Limiter();
        limiter.TryAcquire("a", 1, Window, out _);

        Assert.True(limiter.TryAcquire("b", 1, Window, out _));
    }
}
