using System.Text.RegularExpressions;
using Crm.Application.Common.Localization;

namespace Crm.Application.Ai;

/// <summary>
/// Settings of the AI features, bound from the <c>Ai</c> configuration section. <see cref="ApiKey"/> is a secret
/// (user-secrets or environment variable <c>Ai__ApiKey</c>, never a committed file).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public const string DefaultModel = "claude-haiku-4-5-20251001";
    public const string DefaultBaseUrl = "https://api.anthropic.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = DefaultModel;

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>Suggestions at or above this confidence (0..1) are applied automatically (CRM-52) / answered by the chatbot (CRM-54).</summary>
    public double ConfidenceThreshold { get; set; } = 0.8;

    public int TimeoutSeconds { get; set; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>One text completion: a system instruction and the user content.</summary>
public sealed record AiRequest(string System, string User, int MaxTokens = 1024);

/// <summary>
/// The text generation provider (CRM-50..54). The Infrastructure implementation calls the Claude Messages API; tests use a fake.
/// Failures: <see cref="AiNotConfiguredException"/> (no key; the API answers 503) and <see cref="AiFailedException"/> (the
/// provider failed; the API answers 502).
/// </summary>
public interface IAiTextService
{
    /// <summary>False while no API key is configured: AI actions are then hidden in the UI.</summary>
    bool IsConfigured { get; }

    Task<string> CompleteAsync(AiRequest request, CancellationToken cancellationToken);
}

/// <summary>No API key is configured (503).</summary>
public sealed class AiNotConfiguredException() : Exception(AiText.NotConfigured);

/// <summary>The AI provider failed or answered nothing usable (502). The message never contains customer data.</summary>
public sealed class AiFailedException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>User-facing text of the AI features, in the request language.</summary>
public static class AiText
{
    public static string NotConfigured => LocalizedText.Get(
        "AI is not configured.",
        "الذكاء الاصطناعي غير مُعدّ.");

    public static string Failed => LocalizedText.Get(
        "The AI service could not complete the request. Try again later.",
        "تعذّر على خدمة الذكاء الاصطناعي إكمال الطلب. حاول مرة أخرى لاحقاً.");

    public static string NotConfiguredTitle => LocalizedText.Get("AI not configured", "الذكاء الاصطناعي غير مُعدّ");

    public static string FailedTitle => LocalizedText.Get("AI request failed", "فشل طلب الذكاء الاصطناعي");
}

/// <summary>
/// Hides personal data before text goes to the AI provider (CRM-50 AC 4): email addresses become "[email]" and phone numbers
/// (at least seven digits, Arabic-Indic digits included) "[phone]". Dates such as 2026-10-06 are left alone.
/// </summary>
public static partial class PiiMasker
{
    private const int MinPhoneDigits = 7;

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<![\w])\+?\(?[\d٠-٩][\d٠-٩\s().\-]{5,}[\d٠-٩](?![\w])")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex IsoDatePattern();

    public static string Mask(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var masked = EmailPattern().Replace(text, "[email]");
        return PhonePattern().Replace(masked, match =>
            match.Value.Count(char.IsDigit) >= MinPhoneDigits && !IsoDatePattern().IsMatch(match.Value) ? "[phone]" : match.Value);
    }
}

/// <summary>The language of a text, by script: "ar" when Arabic letters outnumber Latin letters, otherwise "en".</summary>
public static class AiLanguage
{
    public static string Detect(IEnumerable<string?> texts, string? fallback = null)
    {
        var (arabic, latin) = Count(texts);
        if (arabic == 0 && latin == 0 && fallback is not null)
        {
            (arabic, latin) = Count([fallback]);
        }

        return arabic > latin ? LocalizedText.Arabic : LocalizedText.English;
    }

    /// <summary>"Arabic" / "English" for prompts.</summary>
    public static string Name(string language) => language == LocalizedText.Arabic ? "Arabic" : "English";

    private static (int Arabic, int Latin) Count(IEnumerable<string?> texts)
    {
        int arabic = 0, latin = 0;
        foreach (var character in texts.Where(t => t is not null).SelectMany(t => t!))
        {
            if (character is (>= '؀' and <= 'ۿ') or (>= 'ݐ' and <= 'ݿ') or (>= 'ﭐ' and <= '﷿') or (>= 'ﹰ' and <= '﻿'))
            {
                if (char.IsLetter(character))
                {
                    arabic++;
                }
            }
            else if (char.IsAsciiLetter(character))
            {
                latin++;
            }
        }

        return (arabic, latin);
    }
}
