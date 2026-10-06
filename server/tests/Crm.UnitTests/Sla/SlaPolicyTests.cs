using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Sla;

public class SlaPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Defaults_CoverEveryPriority_WithValidTimes()
    {
        Assert.Equal(Enum.GetValues<TicketPriority>(), SlaPolicy.Defaults.Select(d => d.Priority));
        Assert.All(SlaPolicy.Defaults, d => Assert.True(SlaPolicy.AreValid(d.ResponseMinutes, d.ResolutionMinutes)));
    }

    [Fact]
    public void Update_SavesTheValues_AndMovesUpdatedAt()
    {
        var policy = SlaPolicy.Create(TicketPriority.High, 120, 480, Now);

        policy.Update(60, 240, Now.AddHours(1));

        Assert.Equal(TicketPriority.High, policy.Priority);
        Assert.Equal(60, policy.ResponseMinutes);
        Assert.Equal(240, policy.ResolutionMinutes);
        Assert.Equal(Now.AddHours(1), policy.UpdatedAt);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    [InlineData(10, 0)]
    [InlineData(60, 30)]
    [InlineData(SlaPolicy.MaxMinutes + 1, SlaPolicy.MaxMinutes + 1)]
    public void Update_WithInvalidTimes_Throws(int response, int resolution)
    {
        var policy = SlaPolicy.Create(TicketPriority.Mid, 240, 1440, Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => policy.Update(response, resolution, Now));
        Assert.Equal(240, policy.ResponseMinutes);
    }

    [Fact]
    public void Update_ResolutionEqualToResponse_IsAllowed()
    {
        var policy = SlaPolicy.Create(TicketPriority.Low, 480, 4320, Now);

        policy.Update(30, 30, Now);

        Assert.Equal(30, policy.ResolutionMinutes);
    }

    [Fact]
    public void Create_WithANonUtcTime_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SlaPolicy.Create(TicketPriority.High, 60, 240, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    [Fact]
    public void DueTimes_AddTheMinutesToTheStart()
    {
        var policy = SlaPolicy.Create(TicketPriority.High, 60, 240, Now);

        Assert.Equal(Now.AddHours(1), policy.ResponseDueAt(Now));
        Assert.Equal(Now.AddHours(4), policy.ResolutionDueAt(Now));
    }
}
