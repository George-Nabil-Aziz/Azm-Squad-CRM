using System.Text.Json;
using Crm.Application.WebForms;

namespace Crm.Infrastructure.WebForms;

/// <summary>
/// Checks the captcha answer with the provider's <c>siteverify</c> endpoint (form post of <c>secret</c>, <c>response</c>,
/// <c>remoteip</c>; JSON answer with <c>success</c>) — the format of Cloudflare Turnstile, reCAPTCHA and hCaptcha.
/// Without a configured secret the check is skipped; a provider error or timeout counts as a failed check.
/// </summary>
public sealed class HttpCaptchaVerifier(HttpClient http, WebFormOptions options) : ICaptchaVerifier
{
    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        if (!options.CaptchaConfigured)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var form = new Dictionary<string, string> { ["secret"] = options.CaptchaSecret!, ["response"] = token };
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            form["remoteip"] = remoteIp;
        }

        try
        {
            using var response = await http.PostAsync(options.CaptchaVerifyUrl, new FormUrlEncodedContent(form), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return json.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
