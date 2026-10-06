using Crm.Application.Audit;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Audit;


namespace Crm.UnitTests.Audit;

public class AuditLoggerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = Guid.NewGuid();
    private readonly FakeAuditLogRepository _repository = new();
    private readonly AuditClock _clock = new(Now);

    private AuditLogger CreateLogger(Guid? actor) =>
        new(_repository, new AuditActor(actor), new FakeClientInfo("10.1.2.3"), _clock);

    [Fact]
    public async Task Log_StampsUserTimeAndIp_AndSerializesOldAndNewValues()
    {
        await CreateLogger(ActorId).LogAsync(
            new AuditEvent(AuditActions.SlaPolicyUpdated, "SlaPolicy", "high",
                new { responseMinutes = 120 }, new { responseMinutes = 60 }),
            CancellationToken.None);

        var entry = Assert.Single(_repository.Added);
        Assert.Equal(ActorId, entry.UserId);
        Assert.Equal("sla-policy.updated", entry.Action);
        Assert.Equal("SlaPolicy", entry.EntityType);
        Assert.Equal("high", entry.EntityId);
        Assert.Equal("""{"responseMinutes":120}""", entry.OldValues);
        Assert.Equal("""{"responseMinutes":60}""", entry.NewValues);
        Assert.Equal("10.1.2.3", entry.IpAddress);
        Assert.Equal(Now.UtcDateTime, entry.OccurredAt);
        Assert.Equal(DateTimeKind.Utc, entry.OccurredAt.Kind);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Log_WithAnExplicitUser_UsesItInsteadOfTheSignedInUser()
    {
        var loginUser = Guid.NewGuid();

        await CreateLogger(null).LogAsync(
            new AuditEvent(AuditActions.LoginFailed, "User", null, UserId: loginUser, UserEmail: "agent@crm.local"),
            CancellationToken.None);

        var entry = Assert.Single(_repository.Added);
        Assert.Equal(loginUser, entry.UserId);
        Assert.Equal("agent@crm.local", entry.UserEmail);
        Assert.Null(entry.OldValues);
    }

    [Fact]
    public void Entry_WithoutAnAction_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLogEntry.Create(Now.UtcDateTime, null, null, " ", "User", null, null, null, null));
    }

    [Fact]
    public void Entry_WithANonUtcTime_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLogEntry.Create(DateTime.Now, null, null, "user.created", "User", null, null, null, null));
    }
}

public class AuditLogServiceTests
{
    private readonly FakeAuditLogRepository _repository = new();
    private readonly AuditLogService _service;

    public AuditLogServiceTests()
    {
        _service = new AuditLogService(_repository, new ListAuditLogsQueryValidator());
    }

    private Task ListAsync(ListAuditLogsQuery query) => _service.ListAsync(query, CancellationToken.None);

    [Fact]
    public async Task List_UsesPagingDefaults_AndPassesTheFilters()
    {
        var user = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        await ListAsync(new ListAuditLogsQuery(user, "user.created", from, to, null, null));

        var (filter, page, pageSize) = _repository.LastList!.Value;
        Assert.Equal(new AuditLogFilter(user, "user.created", from.UtcDateTime, to.UtcDateTime), filter);
        Assert.Equal((1, 20), (page, pageSize));
    }

    [Fact]
    public async Task List_WithFromAfterTo_ThrowsValidationException_OnFrom()
    {
        var from = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            ListAsync(new ListAuditLogsQuery(null, null, from, from.AddDays(-1), null, null)));

        Assert.Contains("from", error.Errors.Keys);
        Assert.Null(_repository.LastList);
    }

    [Fact]
    public async Task List_WithAnUnknownAction_ThrowsValidationException_OnAction()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            ListAsync(new ListAuditLogsQuery(null, "made.up", null, null, null, null)));

        Assert.Contains("action", error.Errors.Keys);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_WithPagingOutOfRange_ThrowsValidationException(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            ListAsync(new ListAuditLogsQuery(null, null, null, null, page, pageSize)));
    }
}
