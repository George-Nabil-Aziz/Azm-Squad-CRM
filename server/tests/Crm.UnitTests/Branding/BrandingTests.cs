using Crm.Application.Branding;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Files;
using Crm.Application.Settings;
using Crm.Domain.Audit;
using Crm.Domain.Settings;
using Crm.UnitTests.Audit;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Branding;

public class BrandingRulesTests
{
    [Theory]
    [InlineData("#fff", true)]
    [InlineData("#1A2b3C", true)]
    [InlineData(" #123456 ", true)]
    [InlineData("123456", false)]
    [InlineData("#12345", false)]
    [InlineData("#gggggg", false)]
    [InlineData("red", false)]
    [InlineData("rgb(1,2,3)", false)]
    [InlineData("#12345678", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Colors_AreThreeOrSixDigitHex(string? color, bool valid) =>
        Assert.Equal(valid, BrandingRules.IsValidColor(color));

    [Fact]
    public void NormalizeColor_TrimsAndLowerCases() => Assert.Equal("#1a2b3c", BrandingRules.NormalizeColor(" #1A2B3C "));

    [Theory]
    [InlineData("logo.png", "image/png")]
    [InlineData("LOGO.JPG", "image/jpeg")]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.gif", "image/gif")]
    public void AllowedExtensions_GiveTheirContentType(string name, string type)
    {
        Assert.True(BrandingRules.TryGetContentType(name, out var found));
        Assert.Equal(type, found);
    }

    [Theory]
    [InlineData("logo.svg")]
    [InlineData("logo.exe")]
    [InlineData("logo")]
    [InlineData(null)]
    public void OtherFiles_AreRefused(string? name) => Assert.False(BrandingRules.TryGetContentType(name, out _));

    [Fact]
    public void Signatures_AreChecked()
    {
        Assert.True(BrandingRules.MatchesSignature("image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
        Assert.True(BrandingRules.MatchesSignature("image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.True(BrandingRules.MatchesSignature("image/gif", "GIF89a"u8.ToArray()));
        Assert.True(BrandingRules.MatchesSignature("image/webp", "RIFF\0\0\0\0WEBP"u8.ToArray()));
        Assert.False(BrandingRules.MatchesSignature("image/png", "<svg xmlns=''>"u8.ToArray()));
        Assert.False(BrandingRules.MatchesSignature("image/jpeg", []));
    }

    [Fact]
    public void TheLogoLimit_IsTwoMegabytes() => Assert.Equal(2_097_152, BrandingRules.MaxLogoBytes);
}

public class BrandingServiceTests
{
    private sealed class FakeSettings : ISettingsRepository
    {
        public List<SystemSetting> Stored { get; } = [];

        public Task<IReadOnlyList<SystemSetting>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemSetting>>([.. Stored]);

        public void Add(SystemSetting setting) => Stored.Add(setting);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeFiles : IFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            Files[key] = copy.ToArray();
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Files.Remove(key);
            return Task.CompletedTask;
        }
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private readonly FakeSettings _settings = new();
    private readonly FakeFiles _files = new();
    private readonly FakeAuditLogger _audit = new();
    private readonly BrandingService _service;

    public BrandingServiceTests()
    {
        _service = new BrandingService(_settings, _files, new TestClock(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)), _audit);
    }

    private static UploadLogoRequest Logo(string name, byte[] bytes, long? length = null) =>
        new(name, length ?? bytes.Length, new MemoryStream(bytes));

    [Fact]
    public async Task WithoutBranding_EverythingIsTheDefault()
    {
        var branding = await _service.GetAsync(CancellationToken.None);

        Assert.Equal(new BrandingResponse(null, null, null), branding);
        Assert.Null(await _service.OpenLogoAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Update_StoresNormalizedColors_AndAuditsIt()
    {
        var branding = await _service.UpdateAsync(new UpdateBrandingRequest(" #1A2B3C ", "#FFF"), CancellationToken.None);

        Assert.Equal(("#1a2b3c", "#fff"), (branding.PrimaryColor, branding.SecondaryColor));
        Assert.Equal(branding, await _service.GetAsync(CancellationToken.None));
        Assert.Equal(AuditActions.BrandingUpdated, Assert.Single(_audit.Events).Action);
    }

    [Fact]
    public async Task Update_WithEmptyColors_ResetsToTheDefaults()
    {
        await _service.UpdateAsync(new UpdateBrandingRequest("#111111", "#222222"), CancellationToken.None);

        var branding = await _service.UpdateAsync(new UpdateBrandingRequest("", null), CancellationToken.None);

        Assert.Equal(new BrandingResponse(null, null, null), branding);
    }

    [Fact]
    public async Task Update_WithAnInvalidColor_ThrowsValidationException_OnTheField()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(new UpdateBrandingRequest("blue", "#12"), CancellationToken.None));

        Assert.Equal(["primaryColor", "secondaryColor"], error.Errors.Keys.Order());
        Assert.Empty(_settings.Stored);
        Assert.Empty(_audit.Events);
    }

    [Fact]
    public async Task UploadLogo_StoresTheFile_AndReturnsItsUrl()
    {
        var branding = await _service.UploadLogoAsync(Logo("logo.png", Png), CancellationToken.None);

        Assert.StartsWith("/api/branding/logo?v=", branding.LogoUrl);
        var logo = await _service.OpenLogoAsync(CancellationToken.None);
        Assert.Equal("image/png", logo!.ContentType);
        using var copy = new MemoryStream();
        await logo.Content.CopyToAsync(copy);
        Assert.Equal(Png, copy.ToArray());
        Assert.Single(_audit.Events);
    }

    [Fact]
    public async Task UploadingAgain_ReplacesTheLogo_AndDeletesTheOldFile()
    {
        var first = await _service.UploadLogoAsync(Logo("a.png", Png), CancellationToken.None);

        var second = await _service.UploadLogoAsync(Logo("b.png", Png), CancellationToken.None);

        Assert.NotEqual(first.LogoUrl, second.LogoUrl);
        Assert.Single(_files.Files);
    }

    [Fact]
    public async Task Logo_AtExactlyTwoMegabytes_IsAccepted_OneByteMoreIsRefused()
    {
        var ok = Logo("a.png", Png, BrandingRules.MaxLogoBytes);
        await _service.UploadLogoAsync(ok, CancellationToken.None);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UploadLogoAsync(Logo("b.png", Png, BrandingRules.MaxLogoBytes + 1), CancellationToken.None));

        Assert.Equal(["file"], error.Errors.Keys);
    }

    [Theory]
    [InlineData("logo.svg")]
    [InlineData("logo.exe")]
    [InlineData(null)]
    public async Task Logo_OfAnotherType_IsRefused(string? name)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => _service.UploadLogoAsync(Logo(name!, Png), CancellationToken.None));

        Assert.Equal(["file"], error.Errors.Keys);
        Assert.Empty(_files.Files);
    }

    [Fact]
    public async Task Logo_WhoseContentIsNotAnImage_OrIsEmpty_IsRefused()
    {
        var fake = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UploadLogoAsync(Logo("logo.png", "<svg onload=alert(1)>"u8.ToArray()), CancellationToken.None));
        var empty = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UploadLogoAsync(new UploadLogoRequest("logo.png", 0, new MemoryStream()), CancellationToken.None));
        var missing = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UploadLogoAsync(new UploadLogoRequest(null, 0, null), CancellationToken.None));

        Assert.All([fake, empty, missing], e => Assert.Equal(["file"], e.Errors.Keys));
        Assert.Empty(_files.Files);
    }

    [Fact]
    public async Task RemoveLogo_DeletesTheFile_AndGoesBackToNoLogo()
    {
        await _service.UploadLogoAsync(Logo("logo.png", Png), CancellationToken.None);

        var branding = await _service.RemoveLogoAsync(CancellationToken.None);

        Assert.Null(branding.LogoUrl);
        Assert.Empty(_files.Files);
        Assert.Null(await _service.OpenLogoAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EmailBranding_CarriesColorsAndTheLogoBytes()
    {
        await _service.UpdateAsync(new UpdateBrandingRequest("#112233", null), CancellationToken.None);
        await _service.UploadLogoAsync(Logo("logo.png", Png), CancellationToken.None);

        var email = await _service.GetEmailBrandingAsync(CancellationToken.None);

        Assert.Equal(("#112233", null), (email.PrimaryColor, email.SecondaryColor));
        Assert.Equal(Png, email.Logo);
        Assert.Equal("image/png", email.LogoContentType);
    }
}
