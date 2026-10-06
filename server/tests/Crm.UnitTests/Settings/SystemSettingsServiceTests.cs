using System.Text.Json;
using Crm.Application.Common.Exceptions;
using Crm.Application.Settings;
using Crm.Domain.Settings;
using Crm.UnitTests.Audit;

namespace Crm.UnitTests.Settings;

public class SystemSettingsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeSettingsRepository _repository = new();
    private readonly FakeAuditLogger _audit = new();
    private readonly FakeChannelSettingsApplier _applier = new();
    private readonly SystemSettingsService _service;

    public SystemSettingsServiceTests()
    {
        _service = new SystemSettingsService(
            _repository, new FakeSecretProtector(), _applier, _audit, new AuditClock(Now), new UpdateSettingsRequestValidator());
    }

    private static UpdateSettingsRequest Request(
        BusinessHoursSettings? hours = null,
        string? timeZone = "Asia/Riyadh",
        string? prefix = "TKT-",
        EmailSettings? email = null,
        WhatsAppSettings? whatsApp = null,
        SettingsSecrets? secrets = null) =>
        new(hours, timeZone, prefix, email, whatsApp, secrets);

    private static BusinessHoursSettings Hours(
        bool enabled = true, string[]? days = null, string start = "08:00", string end = "16:00") =>
        new(enabled, days ?? ["sunday", "monday", "tuesday", "wednesday", "thursday"], start, end);

    [Fact]
    public async Task Get_WithNothingStored_ReturnsTheDefaults()
    {
        var settings = await _service.GetAsync(CancellationToken.None);

        Assert.False(settings.BusinessHours.Enabled);
        Assert.Equal(["sunday", "monday", "tuesday", "wednesday", "thursday"], settings.BusinessHours.Days);
        Assert.Equal(("08:00", "16:00"), (settings.BusinessHours.Start, settings.BusinessHours.End));
        Assert.Equal("Asia/Riyadh", settings.TimeZone);
        Assert.Equal("TKT-", settings.TicketPrefix);
        Assert.Equal((587, "StartTls"), (settings.Email.SmtpPort, settings.Email.SmtpSecurity));
        Assert.Equal("INBOX", settings.Email.ImapFolder);
        Assert.Equal(new SecretsSet(false, false, false, false, false), settings.SecretsSet);
    }

    [Fact]
    public async Task Update_SavesBusinessHoursTimeZoneAndPrefix_AndReturnsThem()
    {
        var response = await _service.UpdateAsync(
            Request(Hours(true, ["sunday", "tuesday"], "09:30", "17:00"), "Europe/London", "sup"), CancellationToken.None);

        Assert.Equal(["sunday", "tuesday"], response.BusinessHours.Days);
        Assert.Equal(("09:30", "17:00"), (response.BusinessHours.Start, response.BusinessHours.End));
        Assert.Equal("Europe/London", response.TimeZone);
        Assert.Equal("SUP-", response.TicketPrefix); // normalized
        Assert.Equal(JsonSerializer.Serialize(response), JsonSerializer.Serialize(await _service.GetAsync(CancellationToken.None)));
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Update_WithNullSections_KeepsWhatIsStored()
    {
        await _service.UpdateAsync(Request(Hours(true), "Europe/London", "SUP-"), CancellationToken.None);

        var response = await _service.UpdateAsync(Request(null, null, null), CancellationToken.None);

        Assert.True(response.BusinessHours.Enabled);
        Assert.Equal("Europe/London", response.TimeZone);
        Assert.Equal("SUP-", response.TicketPrefix);
    }

    [Fact]
    public async Task Update_SavesEmailAndWhatsAppSettings()
    {
        var response = await _service.UpdateAsync(Request(
            email: new EmailSettings("support@azm.example", "AZM Support", "smtp.azm.example", 465, "SslOnConnect", "smtp-user",
                "imap.azm.example", 993, "SslOnConnect", "imap-user", "INBOX"),
            whatsApp: new WhatsAppSettings("12345")), CancellationToken.None);

        Assert.Equal("smtp.azm.example", response.Email.SmtpHost);
        Assert.Equal(465, response.Email.SmtpPort);
        Assert.Equal("imap-user", response.Email.ImapUserName);
        Assert.Equal("12345", response.WhatsApp.PhoneNumberId);
    }

    [Fact]
    public async Task Secrets_AreStoredEncrypted_AndNeverReturned()
    {
        var response = await _service.UpdateAsync(Request(secrets: new SettingsSecrets(
            "smtp-secret-pw", "imap-secret-pw", "wa-secret-token", "wa-app-secret", "wa-verify")), CancellationToken.None);

        Assert.Equal(new SecretsSet(true, true, true, true, true), response.SecretsSet);
        var stored = _repository.Settings.Where(s => s.IsSecret).ToList();
        Assert.Equal(5, stored.Count);
        Assert.All(stored, s => Assert.StartsWith(FakeSecretProtector.Marker, s.Value));
        var everything = JsonSerializer.Serialize(response) + JsonSerializer.Serialize(await _service.GetAsync(CancellationToken.None));
        foreach (var secret in new[] { "smtp-secret-pw", "imap-secret-pw", "wa-secret-token", "wa-app-secret", "wa-verify" })
        {
            Assert.DoesNotContain(secret, everything);
            Assert.DoesNotContain(secret, string.Join('|', _repository.Settings.Select(s => s.Value)));
        }
    }

    [Fact]
    public async Task Secrets_NullKeeps_EmptyClears_TextReplaces()
    {
        await _service.UpdateAsync(Request(secrets: new SettingsSecrets("one", "two", null, null, null)), CancellationToken.None);

        var kept = await _service.UpdateAsync(Request(secrets: new SettingsSecrets(null, null, null, null, null)), CancellationToken.None);
        Assert.True(kept.SecretsSet.SmtpPassword);
        Assert.True(kept.SecretsSet.ImapPassword);

        var changed = await _service.UpdateAsync(Request(secrets: new SettingsSecrets("", "three", null, null, null)), CancellationToken.None);
        Assert.False(changed.SecretsSet.SmtpPassword);
        Assert.True(changed.SecretsSet.ImapPassword);
        Assert.Equal("three", new FakeSecretProtector().Unprotect(_repository.Settings.Single(s => s.Key == SettingKeys.ImapPassword).Value!));
    }

    [Fact]
    public async Task Update_WritesAnAuditEvent_WithOldAndNewValues_ButNoSecrets()
    {
        await _service.UpdateAsync(Request(Hours(true), "Asia/Riyadh", "SUP-", secrets: new SettingsSecrets("super-secret", null, null, null, null)),
            CancellationToken.None);

        var logged = Assert.Single(_audit.Events);
        Assert.Equal("settings.updated", logged.Action);
        Assert.Equal("SystemSettings", logged.EntityType);
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var old = JsonSerializer.Serialize(logged.OldValues, web);
        var changed = JsonSerializer.Serialize(logged.NewValues, web);
        Assert.Contains("\"ticketPrefix\":\"TKT-\"", old);
        Assert.Contains("\"ticketPrefix\":\"SUP-\"", changed);
        Assert.Contains("smtpPassword", changed); // which secret changed ...
        Assert.DoesNotContain("super-secret", old + changed); // ... never its value
    }

    [Fact]
    public async Task Update_AppliesTheChannelSettings_AfterSaving()
    {
        await _service.UpdateAsync(Request(), CancellationToken.None);

        Assert.Equal(1, _applier.Count);
    }

    [Theory]
    [InlineData("businessHours.days", "none")]
    [InlineData("businessHours.days", "funday")]
    [InlineData("businessHours.start", "8am")]
    [InlineData("businessHours.end", "25:00")]
    [InlineData("businessHours.end", "07:00")] // before the start
    [InlineData("timeZone", "Mars/Olympus")]
    [InlineData("ticketPrefix", "TOO-LONG-PREFIX")]
    [InlineData("ticketPrefix", "a b")]
    [InlineData("email.smtpPort", "0")]
    [InlineData("email.imapPort", "70000")]
    [InlineData("email.smtpSecurity", "Maybe")]
    [InlineData("email.fromAddress", "not-an-email")]
    public async Task Update_WithInvalidValues_ThrowsValidationException_AndSavesNothing(string field, string kind)
    {
        var request = field switch
        {
            "businessHours.days" => Request(Hours(days: kind == "none" ? [] : ["funday"])),
            "businessHours.start" => Request(Hours(start: kind)),
            "businessHours.end" => Request(Hours(end: kind)),
            "timeZone" => Request(timeZone: kind),
            "ticketPrefix" => Request(prefix: kind),
            "email.smtpPort" => Request(email: Email(smtpPort: int.Parse(kind))),
            "email.imapPort" => Request(email: Email(imapPort: int.Parse(kind))),
            "email.smtpSecurity" => Request(email: Email(smtpSecurity: kind)),
            _ => Request(email: Email(fromAddress: kind)),
        };

        var error = await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateAsync(request, CancellationToken.None));

        Assert.Contains(field, error.Errors.Keys);
        Assert.Equal(0, _repository.SaveCount);
        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task DaysAreNotRequired_WhenBusinessHoursAreOff()
    {
        var response = await _service.UpdateAsync(Request(Hours(false, [])), CancellationToken.None);

        Assert.False(response.BusinessHours.Enabled);
    }

    [Fact]
    public async Task Provider_WithBusinessHoursOff_HasNoCalendar_AndTheStoredPrefix()
    {
        await _service.UpdateAsync(Request(Hours(false), prefix: "sup"), CancellationToken.None);

        var runtime = await new SystemSettingsProvider(_repository, new FakeSecretProtector()).GetAsync(CancellationToken.None);

        Assert.Null(runtime.Calendar);
        Assert.Equal("SUP-", runtime.TicketPrefix);
    }

    [Fact]
    public async Task Provider_WithBusinessHoursOn_BuildsTheCalendar()
    {
        await _service.UpdateAsync(Request(Hours(true, ["monday"], "09:00", "13:00"), "UTC"), CancellationToken.None);

        var runtime = await new SystemSettingsProvider(_repository, new FakeSecretProtector()).GetAsync(CancellationToken.None);

        Assert.NotNull(runtime.Calendar);
        Assert.Equal([DayOfWeek.Monday], runtime.Calendar.Days);
        Assert.Equal((new TimeOnly(9, 0), new TimeOnly(13, 0)), (runtime.Calendar.Start, runtime.Calendar.End));
        Assert.Equal("UTC", runtime.Calendar.TimeZone.Id);
    }

    [Fact]
    public async Task Provider_WithNothingStored_IsTwentyFourSeven_WithTheDefaultPrefix()
    {
        var runtime = await new SystemSettingsProvider(_repository, new FakeSecretProtector()).GetAsync(CancellationToken.None);

        Assert.Null(runtime.Calendar);
        Assert.Equal("TKT-", runtime.TicketPrefix);
    }

    private static EmailSettings Email(int smtpPort = 587, int imapPort = 993, string smtpSecurity = "StartTls", string? fromAddress = null) =>
        new(fromAddress, null, "smtp.example", smtpPort, smtpSecurity, null, "imap.example", imapPort, "SslOnConnect", null, "INBOX");
}

internal sealed class FakeSettingsRepository : ISettingsRepository
{
    public List<SystemSetting> Settings { get; } = [];

    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<SystemSetting>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SystemSetting>>([.. Settings]);

    public void Add(SystemSetting setting) => Settings.Add(setting);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>"Encrypts" by prefixing and reversing, so a stored value is never the plain text.</summary>
internal sealed class FakeSecretProtector : ISecretProtector
{
    public const string Marker = "enc:";

    public string Protect(string plainText) => Marker + new string([.. plainText.Reverse()]);

    public string? Unprotect(string protectedText) =>
        protectedText.StartsWith(Marker, StringComparison.Ordinal) ? new string([.. protectedText[Marker.Length..].Reverse()]) : null;
}

internal sealed class FakeChannelSettingsApplier : IChannelSettingsApplier
{
    public int Count { get; private set; }

    public Task ApplyAsync(CancellationToken cancellationToken)
    {
        Count++;
        return Task.CompletedTask;
    }
}

/// <summary>Settings a test sets by hand: business hours (default off = 24/7) and the ticket prefix (default "TKT-").</summary>
internal sealed class FakeSystemSettingsProvider : ISystemSettingsProvider
{
    public Crm.Domain.Sla.BusinessCalendar? Calendar { get; set; }

    public string TicketPrefix { get; set; } = "TKT-";

    public Task<RuntimeSettings> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new RuntimeSettings(Calendar, TicketPrefix));
}
