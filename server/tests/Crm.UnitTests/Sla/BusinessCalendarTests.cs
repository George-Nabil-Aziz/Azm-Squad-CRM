using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Sla;

public class BusinessCalendarTests
{
    // UTC+3 without daylight saving; Sunday to Thursday, 08:00-16:00 local = 05:00-13:00 UTC.
    private static readonly TimeZoneInfo Riyadh = TimeZoneInfo.CreateCustomTimeZone("test-riyadh", TimeSpan.FromHours(3), "Test Riyadh", "Test Riyadh");
    private static readonly DayOfWeek[] WorkDays = [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday];
    private static readonly BusinessCalendar Calendar = new(WorkDays, new TimeOnly(8, 0), new TimeOnly(16, 0), Riyadh);

    // 2026-10-06 is a Tuesday, 2026-10-08 a Thursday, 2026-10-09 a Friday, 2026-10-11 a Sunday.
    private static DateTime Utc(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void InsideTheHours_JustAddsTheMinutes()
    {
        Assert.Equal(Utc(6, 9), Calendar.AddBusinessMinutes(Utc(6, 7), 120));
    }

    [Fact]
    public void ReachingTheEndOfTheDayExactly_StaysOnThatDay()
    {
        Assert.Equal(Utc(6, 13), Calendar.AddBusinessMinutes(Utc(6, 11), 120));
    }

    [Fact]
    public void SpillingOverTheEndOfTheDay_ContinuesAtTheNextOpening()
    {
        // Tue 14:00 local: 120 min left today, 60 min on Wednesday from 08:00 → Wed 09:00 local = 06:00 UTC.
        Assert.Equal(Utc(7, 6), Calendar.AddBusinessMinutes(Utc(6, 11), 180));
    }

    [Fact]
    public void StartingBeforeOpening_CountsFromOpening()
    {
        Assert.Equal(Utc(6, 6), Calendar.AddBusinessMinutes(Utc(6, 3), 60)); // Tue 06:00 local → 09:00 local
    }

    [Fact]
    public void StartingAfterClosing_CountsFromTheNextOpening()
    {
        Assert.Equal(Utc(7, 6), Calendar.AddBusinessMinutes(Utc(6, 14), 60)); // Tue 17:00 local → Wed 09:00 local
    }

    [Fact]
    public void WeekendDays_AreSkipped()
    {
        // Thu 15:00 local: 60 min left, then Friday and Saturday are closed → Sunday 09:00 local.
        Assert.Equal(Utc(11, 6), Calendar.AddBusinessMinutes(Utc(8, 12), 120));
    }

    [Fact]
    public void StartingOnAClosedDay_CountsFromTheNextWorkingDay()
    {
        Assert.Equal(Utc(11, 6), Calendar.AddBusinessMinutes(Utc(9, 7), 60)); // Friday → Sunday 09:00 local
    }

    [Fact]
    public void ThreeWorkingDays_Are1440BusinessMinutes()
    {
        Assert.Equal(Utc(8, 13), Calendar.AddBusinessMinutes(Utc(6, 5), 1440)); // Tue 08:00 → Thu 16:00 local
    }

    [Fact]
    public void FractionalMinutes_AreSupported()
    {
        Assert.Equal(Utc(6, 7, 1).AddSeconds(30), Calendar.AddBusinessMinutes(Utc(6, 7), 1.5));
    }

    [Fact]
    public void TheTimeZone_ShiftsTheWindow()
    {
        var utc = new BusinessCalendar(WorkDays, new TimeOnly(8, 0), new TimeOnly(16, 0), TimeZoneInfo.Utc);

        // 08:00-16:00 UTC: Tue 14:00 UTC + 180 min → 120 today, 60 on Wednesday from 08:00 UTC.
        Assert.Equal(Utc(7, 9), utc.AddBusinessMinutes(Utc(6, 14), 180));
    }

    [Fact]
    public void ADayInTheMiddleOfASpringForwardGap_DoesNotThrow()
    {
        var gapZone = TimeZoneInfo.CreateCustomTimeZone(
            "test-dst", TimeSpan.Zero, "Test DST", "Test DST", "Test DST DST",
            [TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 10, 6),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 3, 0, 0), 11, 1))]);
        var calendar = new BusinessCalendar(WorkDays, new TimeOnly(1, 30), new TimeOnly(5, 0), gapZone);

        var due = calendar.AddBusinessMinutes(Utc(5, 22), 60); // Tue 6 Oct: 02:00-03:00 local does not exist

        Assert.True(due > Utc(5, 22));
    }

    [Fact]
    public void WithoutAnyWorkingDay_ItIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new BusinessCalendar([], new TimeOnly(8, 0), new TimeOnly(16, 0), Riyadh));
    }

    [Theory]
    [InlineData(16, 8)]
    [InlineData(8, 8)]
    public void StartNotBeforeEnd_IsRejected(int start, int end)
    {
        Assert.Throws<ArgumentException>(() => new BusinessCalendar(WorkDays, new TimeOnly(start, 0), new TimeOnly(end, 0), Riyadh));
    }

    [Fact]
    public void ANonUtcStart_OrNonPositiveMinutes_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => Calendar.AddBusinessMinutes(DateTime.SpecifyKind(Utc(6, 7), DateTimeKind.Local), 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => Calendar.AddBusinessMinutes(Utc(6, 7), 0));
    }

    [Fact]
    public void ASlaPolicy_UsesTheCalendar_WhenGiven()
    {
        var policy = SlaPolicy.Create(TicketPriority.High, 120, 480, Utc(1, 0));

        Assert.Equal(Utc(6, 7).AddHours(2), policy.ResponseDueAt(Utc(6, 7)));
        Assert.Equal(Utc(6, 9), policy.ResponseDueAt(Utc(6, 7), Calendar));
        Assert.Equal(Utc(6, 13), policy.ResolutionDueAt(Utc(6, 5), Calendar)); // 480 business minutes = the whole day
    }

    [Fact]
    public void ATicket_GetsBusinessHourDueTimes_AndAWarningAt80PercentOfTheBusinessMinutes()
    {
        var created = Utc(6, 11); // Tue 14:00 local
        var ticket = Ticket.Create(Guid.NewGuid(), "Printer", null, null, TicketPriority.High, TicketChannel.Manual, null, created);

        ticket.ApplySla(SlaPolicy.Create(TicketPriority.High, 180, 480, created), Calendar);

        Assert.Equal(Utc(7, 6), ticket.ResponseDueAt); // 180 business minutes → Wed 09:00 local
        Assert.Equal(Utc(7, 5, 24), ticket.ResponseWarningAt); // 144 business minutes → Wed 08:24 local
    }
}
