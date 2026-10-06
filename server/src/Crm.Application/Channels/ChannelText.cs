using Crm.Application.Common.Localization;

namespace Crm.Application.Channels;

/// <summary>User-facing text of the channels feature, in the request language.</summary>
public static class ChannelText
{
    public static string EmailNotConfigured => LocalizedText.Get(
        "Email is not configured.",
        "البريد الإلكتروني غير مُعدّ.");

    public static string WhatsAppNotConfigured => LocalizedText.Get(
        "WhatsApp is not configured.",
        "واتساب غير مُعدّ.");

    public static string ProviderMissing => LocalizedText.Get(
        "This channel cannot send messages.",
        "لا يمكن إرسال الرسائل عبر هذه القناة.");
}
