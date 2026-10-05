namespace Crm.Application.Common.Localization;

/// <summary>ProblemDetails titles in the request language (used by the API's error handling).</summary>
public static class ErrorText
{
    public static string ValidationFailed => LocalizedText.Get(
        "One or more validation errors occurred.",
        "حدث خطأ واحد أو أكثر في التحقق من البيانات.");

    public static string MalformedRequest => LocalizedText.Get(
        "The request is malformed.",
        "صيغة الطلب غير صحيحة.");

    public static string AuthenticationFailed => LocalizedText.Get(
        "Authentication failed.",
        "فشل التحقق من الهوية.");

    public static string AuthenticationRequired => LocalizedText.Get(
        "Authentication is required.",
        "يجب تسجيل الدخول.");

    public static string Forbidden => LocalizedText.Get(
        "You do not have permission to perform this action.",
        "ليست لديك صلاحية لتنفيذ هذا الإجراء.");

    public static string NotFound => LocalizedText.Get(
        "The requested resource was not found.",
        "العنصر المطلوب غير موجود.");

    public static string Conflict => LocalizedText.Get(
        "The request conflicts with the current state.",
        "يتعارض الطلب مع البيانات الحالية.");

    public static string Unexpected => LocalizedText.Get(
        "An unexpected error occurred.",
        "حدث خطأ غير متوقع.");
}
