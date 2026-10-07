using Crm.Application.Common.Exceptions;
using Crm.Application.Reports;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Reports;

public class TicketReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeReportsRepository _repository = new();
    private readonly TicketReportService _service;

    public TicketReportServiceTests()
    {
        _service = new TicketReportService(_repository, new ReportClock(Now));
    }

    private static TicketReportQuery Query(
        string? from = "2026-10-01", string? to = "2026-10-03", string? status = null, Guid? categoryId = null,
        string? channel = null, string? priority = null) =>
        new(from is null ? null : DateOnly.Parse(from), to is null ? null : DateOnly.Parse(to), status, categoryId, channel, priority);

    [Fact]
    public async Task EveryStatusChannelPriorityAndDay_IsListed_WithZeros()
    {
        _repository.TicketCounts = new TicketCounts(
            new Dictionary<TicketStatus, int> { [TicketStatus.Open] = 3, [TicketStatus.Closed] = 1 },
            new Dictionary<TicketChannel, int> { [TicketChannel.Email] = 4 },
            new Dictionary<TicketPriority, int> { [TicketPriority.High] = 2, [TicketPriority.Low] = 2 },
            [new CategoryCountRow(null, null, 1), new CategoryCountRow(Guid.NewGuid(), "Billing", 3)],
            new Dictionary<DateOnly, int> { [new DateOnly(2026, 10, 2)] = 4 });

        var report = await _service.GetAsync(Query(), CancellationToken.None);

        Assert.Equal(4, report.Total);
        Assert.Equal(["new", "open", "pending", "resolved", "closed"], report.ByStatus.Select(c => c.Key));
        Assert.Equal([0, 3, 0, 0, 1], report.ByStatus.Select(c => c.Count));
        Assert.Equal(["manual", "email", "whatsapp", "portal", "webform", "chat"], report.ByChannel.Select(c => c.Key));
        Assert.Equal([0, 4, 0, 0, 0, 0], report.ByChannel.Select(c => c.Count));
        Assert.Equal(["high", "mid", "low"], report.ByPriority.Select(c => c.Key));
        Assert.Equal([2, 0, 2], report.ByPriority.Select(c => c.Count));
        Assert.Equal(["2026-10-01", "2026-10-02", "2026-10-03"], report.ByDay.Select(d => d.Date.ToString("yyyy-MM-dd")));
        Assert.Equal([0, 4, 0], report.ByDay.Select(d => d.Count));
        Assert.Equal(new DateOnly(2026, 10, 1), report.From);
        Assert.Equal(new DateOnly(2026, 10, 3), report.To);
    }

    [Fact]
    public async Task Categories_AreSortedByCount_WithTheUncategorizedBucketLast_WhenTied()
    {
        var billing = Guid.NewGuid();
        var sales = Guid.NewGuid();
        _repository.TicketCounts = _repository.TicketCounts with
        {
            ByCategory = [new CategoryCountRow(null, null, 2), new CategoryCountRow(sales, "Sales", 2), new CategoryCountRow(billing, "Billing", 5)],
        };

        var report = await _service.GetAsync(Query(), CancellationToken.None);

        Assert.Equal([billing, sales, null], report.ByCategory.Select(c => c.CategoryId));
    }

    [Fact]
    public async Task TheRepository_GetsTheUtcRange_AndTheParsedFilters()
    {
        var category = Guid.NewGuid();

        await _service.GetAsync(Query(status: "OPEN", categoryId: category, channel: "WhatsApp", priority: "high"), CancellationToken.None);

        Assert.Equal(
            new TicketReportFilter(
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc),
                TicketStatus.Open, category, TicketChannel.WhatsApp, TicketPriority.High),
            _repository.LastTicketFilter);
    }

    [Fact]
    public async Task WithoutDates_TheLast30DaysAreReported()
    {
        var report = await _service.GetAsync(Query(null, null), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 6), report.To);
        Assert.Equal(30, report.ByDay.Count);
    }

    [Theory]
    [InlineData("status", "archived")]
    [InlineData("channel", "fax")]
    [InlineData("channel", "2")]
    [InlineData("priority", "urgent")]
    public async Task UnknownFilterValues_AreValidationErrors_OnTheField(string field, string value)
    {
        var query = field switch
        {
            "status" => Query(status: value),
            "channel" => Query(channel: value),
            _ => Query(priority: value),
        };

        var error = await Assert.ThrowsAsync<ValidationException>(() => _service.GetAsync(query, CancellationToken.None));

        Assert.Contains(field, error.Errors.Keys);
        Assert.Null(_repository.LastTicketFilter);
    }

    [Fact]
    public async Task Export_WritesOneRowPerBucket_AsCsv()
    {
        _repository.TicketCounts = _repository.TicketCounts with
        {
            ByStatus = new Dictionary<TicketStatus, int> { [TicketStatus.Open] = 2 },
        };

        var file = await _service.ExportAsync(Query(), "csv", CancellationToken.None);

        var text = System.Text.Encoding.UTF8.GetString(file.Content).TrimStart('﻿');
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Report,Value,Count", lines[0]);
        Assert.Contains("Status,open,2", lines);
        Assert.Contains("Status,new,0", lines);
        Assert.Contains("Day,2026-10-02,0", lines);
        Assert.Equal("ticket-report-2026-10-01_2026-10-03.csv", file.FileName);
    }

    [Fact]
    public async Task Export_WithAnUnknownFormat_IsAValidationError()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ExportAsync(Query(), "pdf", CancellationToken.None));
    }
}
