using Crm.Application.Common.Localization;

namespace Crm.Application.Customers;

/// <summary>User-facing text of the customer feature, in the request language.</summary>
public static class CustomerText
{
    public static string NameField => LocalizedText.Get("Name", "الاسم");

    public static string EmailField => LocalizedText.Get("Email", "البريد الإلكتروني");

    public static string PhoneField => LocalizedText.Get("Phone", "رقم الهاتف");

    public static string PhoneInvalid => LocalizedText.Get(
        "Enter a phone number with at least 6 digits; it may start with + and contain spaces, dashes or brackets.",
        "أدخل رقم هاتف من 6 أرقام على الأقل، يمكن أن يبدأ بـ + ويحتوي على مسافات أو شرطات أو أقواس.");

    public static string NotFound => LocalizedText.Get(
        "The customer was not found.",
        "العميل غير موجود.");
}
