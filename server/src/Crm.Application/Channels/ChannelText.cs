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

    public static string WhatsAppWindowClosed => LocalizedText.Get(
        "The last customer message is older than 24 hours. Send an approved template instead.",
        "مرّ أكثر من 24 ساعة على آخر رسالة من العميل. أرسل قالباً معتمداً بدلاً من ذلك.");

    public static string WebhookVerifyTokenInvalid => LocalizedText.Get(
        "The webhook verify token is not valid.",
        "رمز التحقق من الـ Webhook غير صحيح.");

    public static string WebhookSignatureInvalid => LocalizedText.Get(
        "The webhook signature is not valid.",
        "توقيع الـ Webhook غير صحيح.");
}
