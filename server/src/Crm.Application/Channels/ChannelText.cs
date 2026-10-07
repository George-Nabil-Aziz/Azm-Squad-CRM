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

    public static string SmsNotConfigured => LocalizedText.Get(
        "SMS is not configured.",
        "الرسائل النصية غير مُعدّة.");

    public static string CustomerHasNoPhone => LocalizedText.Get(
        "The customer has no phone number.",
        "لا يوجد رقم هاتف للعميل.");

    public static string SmsSubject => LocalizedText.Get("SMS message", "رسالة نصية");

    public static string ProviderMissing => LocalizedText.Get(
        "This channel cannot send messages.",
        "لا يمكن إرسال الرسائل عبر هذه القناة.");

    public static string WhatsAppWindowClosed => LocalizedText.Get(
        "The last customer message is older than 24 hours. Send an approved template instead.",
        "مرّ أكثر من 24 ساعة على آخر رسالة من العميل. أرسل قالباً معتمداً بدلاً من ذلك.");

    public static string CustomerHasNoEmail => LocalizedText.Get(
        "The customer has no email address.",
        "لا يوجد بريد إلكتروني للعميل.");

    public static string CustomerHasNoWhatsApp => LocalizedText.Get(
        "The customer has no WhatsApp or phone number.",
        "لا يوجد رقم واتساب أو هاتف للعميل.");

    public static string NoSubject => LocalizedText.Get("(no subject)", "(بدون عنوان)");

    public static string NoText => LocalizedText.Get("(no text)", "(بدون نص)");

    public static string WhatsAppSubject => LocalizedText.Get("WhatsApp message", "رسالة واتساب");

    public static string WebhookVerifyTokenInvalid => LocalizedText.Get(
        "The webhook verify token is not valid.",
        "رمز التحقق من الـ Webhook غير صحيح.");

    public static string WebhookSignatureInvalid => LocalizedText.Get(
        "The webhook signature is not valid.",
        "توقيع الـ Webhook غير صحيح.");
}
