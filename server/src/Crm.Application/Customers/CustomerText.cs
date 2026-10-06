using Crm.Application.Common.Localization;

namespace Crm.Application.Customers;

/// <summary>User-facing text of the customer feature, in the request language.</summary>
public static class CustomerText
{
    public static string NameField => LocalizedText.Get("Name", "الاسم");

    public static string EmailField => LocalizedText.Get("Email", "البريد الإلكتروني");

    public static string PhoneField => LocalizedText.Get("Phone", "رقم الهاتف");

    public static string PhoneInvalid => LocalizedText.Get(
        "Enter a valid phone number, e.g. +966501234567 or 0501234567.",
        "أدخل رقم هاتف صحيحاً، مثل +966501234567 أو 0501234567.");

    public static string NotFound => LocalizedText.Get(
        "The customer was not found.",
        "العميل غير موجود.");

    public static string ContactTypeField => LocalizedText.Get("Contact type", "نوع جهة الاتصال");

    public static string ContactValueField => LocalizedText.Get("Value", "القيمة");

    public static string ContactTypeInvalid => LocalizedText.Get(
        "Choose phone, email or WhatsApp.",
        "اختر الهاتف أو البريد الإلكتروني أو واتساب.");

    public static string ContactExists => LocalizedText.Get(
        "The customer already has this contact.",
        "جهة الاتصال هذه مسجلة للعميل بالفعل.");

    public static string ContactNotFound => LocalizedText.Get(
        "The contact was not found.",
        "جهة الاتصال غير موجودة.");

    public static string LookupNeedsPhoneOrEmail => LocalizedText.Get(
        "Enter a phone number or an email address.",
        "أدخل رقم هاتف أو بريداً إلكترونياً.");

    public static string LookupPhoneOrEmailOnly => LocalizedText.Get(
        "Look up by phone or by email, not both.",
        "ابحث برقم الهاتف أو بالبريد الإلكتروني، وليس بكليهما.");

    public static string TimelineTypeField => LocalizedText.Get("Type", "النوع");

    public static string TimelineTypeInvalid => LocalizedText.Get(
        "Choose customer, note, attachment, ticket or message.",
        "اختر العميل أو الملاحظة أو المرفق أو التذكرة أو الرسالة.");
}
