using Crm.Application.Common.Localization;

namespace Crm.Application.Users;

/// <summary>User-facing text of the user-management feature, in the request language.</summary>
public static class UserText
{
    public static string EmailField => LocalizedText.Get("Email", "البريد الإلكتروني");

    public static string FullNameField => LocalizedText.Get("Full name", "الاسم الكامل");

    public static string PasswordField => LocalizedText.Get("Password", "كلمة المرور");

    public static string PageField => LocalizedText.Get("Page", "الصفحة");

    public static string PageSizeField => LocalizedText.Get("Page size", "حجم الصفحة");

    public static string EmailTaken => LocalizedText.Get(
        "This email is already used by another user.",
        "هذا البريد الإلكتروني مستخدم من قبل مستخدم آخر.");

    public static string WeakPassword => LocalizedText.Get(
        "Password must be at least 8 characters and contain an upper-case letter, a lower-case letter, a digit and a symbol.",
        "يجب أن تتكون كلمة المرور من 8 أحرف على الأقل وتحتوي على حرف كبير وحرف صغير ورقم ورمز.");

    public static string RolesRequired => LocalizedText.Get(
        "Select at least one role.",
        "اختر دوراً واحداً على الأقل.");

    public static string UnknownRole => LocalizedText.Get(
        "Unknown role.",
        "دور غير معروف.");

    public static string NotFound => LocalizedText.Get(
        "The user was not found.",
        "المستخدم غير موجود.");

    public static string CannotDeactivateSelf => LocalizedText.Get(
        "You cannot deactivate your own account.",
        "لا يمكنك إيقاف حسابك الخاص.");

    public static string SuperAdminOnly => LocalizedText.Get(
        "Only a super administrator can assign the SuperAdmin role or change a super administrator.",
        "لا يمكن إلا لمدير النظام منح دور مدير النظام أو تعديل حساب مدير نظام.");
}
