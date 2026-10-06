using Crm.Application.Common.Localization;

namespace Crm.Application.Portal;

/// <summary>User-facing text of the customer portal, in the request language.</summary>
public static class PortalText
{
    public static string CodeField => LocalizedText.Get("Code", "الرمز");

    // One message for a wrong, expired, used or unknown code: never reveal which one it was.
    public static string InvalidCode => LocalizedText.Get(
        "The code is not valid or has expired. Request a new code.",
        "الرمز غير صحيح أو منتهي الصلاحية. اطلب رمزاً جديداً.");

    public static string CodeEmailSubject => LocalizedText.Get("Your sign-in code", "رمز تسجيل الدخول");

    public static string CodeEmailBody(string code, int minutes) => LocalizedText.Get(
        $"Your sign-in code for the support portal is {code}.\nIt is valid for {minutes} minutes and works once.\nIf you did not ask for it, you can ignore this email.",
        $"رمز تسجيل الدخول إلى بوابة الدعم هو {code}.\nصالح لمدة {minutes} دقائق ويُستخدم مرة واحدة.\nإذا لم تطلبه يمكنك تجاهل هذه الرسالة.");

    public static string CustomerNotFound => LocalizedText.Get(
        "The customer was not found.",
        "العميل غير موجود.");
}
