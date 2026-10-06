using Crm.Application.Common.Exceptions;
using Crm.Application.Sla;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Sla;

public class SlaPolicyServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly FakeSlaPolicyRepository _repository = new(Start.UtcDateTime.AddDays(-1));
    private readonly TestClock _clock = new(Start);
    private readonly Audit.FakeAuditLogger _audit = new();
    private readonly SlaPolicyService _service;

    public SlaPolicyServiceTests()
    {
        _service = new SlaPolicyService(_repository, _clock, new UpdateSlaPolicyRequestValidator(), _audit);
    }

    [Fact]
    public async Task Update_LogsOldAndNewTimes_ToTheAuditLog()
    {
        await UpdateAsync("high", 60, 240);

        var logged = Assert.Single(_audit.Events);
        Assert.Equal("sla-policy.updated", logged.Action);
        Assert.Equal("SlaPolicy", logged.EntityType);
        Assert.Equal("high", logged.EntityId);
        Assert.Equal("""{"responseMinutes":120,"resolutionMinutes":480}""", System.Text.Json.JsonSerializer.Serialize(logged.OldValues));
        Assert.Equal("""{"responseMinutes":60,"resolutionMinutes":240}""", System.Text.Json.JsonSerializer.Serialize(logged.NewValues));
    }

    [Fact]
    public async Task InvalidUpdate_IsNotLogged()
    {
        await Assert.ThrowsAsync<ValidationException>(() => UpdateAsync("high", 0, 240));

        Assert.Empty(_audit.Events);
    }

    private Task<SlaPolicyResponse> UpdateAsync(string priority, int? response, int? resolution) =>
        _service.UpdateAsync(priority, new UpdateSlaPolicyRequest(response, resolution), CancellationToken.None);

    [Fact]
    public async Task List_ReturnsHighMidLow_WithApiNames()
    {
        var list = await _service.ListAsync(CancellationToken.None);

        Assert.Equal(["high", "mid", "low"], list.Select(p => p.Priority));
    }

    [Fact]
    public async Task Update_High_To1hAnd4h_IsSaved()
    {
        var response = await UpdateAsync("high", 60, 240);

        var saved = _repository.Policies.Single(p => p.Priority == TicketPriority.High);
        Assert.Equal((60, 240), (saved.ResponseMinutes, saved.ResolutionMinutes));
        Assert.Equal(new SlaPolicyResponse("high", 60, 240, Start.UtcDateTime), response);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_AcceptsThePriorityInAnyCase()
    {
        var response = await UpdateAsync("MID", 30, 60);

        Assert.Equal("mid", response.Priority);
    }

    [Theory]
    [InlineData(0, 240, "responseMinutes")]
    [InlineData(-1, 240, "responseMinutes")]
    [InlineData(60, 0, "resolutionMinutes")]
    public async Task Update_WithZeroOrNegative_ThrowsValidationException_OnTheField(int response, int resolution, string field)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => UpdateAsync("high", response, resolution));

        Assert.Contains(field, error.Errors.Keys);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_WithResolutionBelowResponse_ThrowsValidationException_OnResolutionMinutes()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => UpdateAsync("high", 240, 60));

        Assert.Equal(["resolutionMinutes"], error.Errors.Keys);
    }

    [Fact]
    public async Task Update_WithMissingValues_ThrowsValidationException()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => UpdateAsync("high", null, null));

        Assert.Equal(["resolutionMinutes", "responseMinutes"], error.Errors.Keys.Order());
    }

    [Theory]
    [InlineData("urgent")]
    [InlineData("1")]
    public async Task Update_UnknownPriority_ThrowsNotFound(string priority)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => UpdateAsync(priority, 60, 240));
    }
}
