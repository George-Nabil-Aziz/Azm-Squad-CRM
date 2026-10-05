using Crm.Application.Common.Localization;

namespace Crm.Application.Auth;

/// <summary>User-facing text of the auth feature, in the request language.</summary>
public static class AuthText
{
    public static string EmailField => LocalizedText.Get("Email", "البريد الإلكتروني");

    public static string PasswordField => LocalizedText.Get("Password", "كلمة المرور");

    // One message for unknown email, wrong password and locked-out user: never reveal which one it was.
    public static string InvalidCredentials => LocalizedText.Get(
        "Invalid email or password.",
        "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
}
