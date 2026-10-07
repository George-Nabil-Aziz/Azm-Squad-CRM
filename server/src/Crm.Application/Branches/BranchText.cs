using Crm.Application.Common.Localization;

namespace Crm.Application.Branches;

/// <summary>User-facing text of the branches feature, in the request language.</summary>
public static class BranchText
{
    public static string NameField => LocalizedText.Get("Name", "الاسم");

    public static string NameTaken => LocalizedText.Get(
        "A branch with this name already exists.",
        "يوجد فرع بهذا الاسم بالفعل.");

    public static string NotFound => LocalizedText.Get("The branch was not found.", "الفرع غير موجود.");

    public static string Unavailable => LocalizedText.Get("Choose an active branch.", "اختر فرعاً نشطاً.");

    public static string NotYours => LocalizedText.Get("You can only use your own branch.", "يمكنك استخدام فرعك فقط.");
}
