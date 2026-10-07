using Crm.Application.Common.Localization;

namespace Crm.Application.Departments;

/// <summary>User-facing text of the departments feature, in the request language.</summary>
public static class DepartmentText
{
    public static string NameField => LocalizedText.Get("Name", "الاسم");

    public static string NameTaken => LocalizedText.Get(
        "A department with this name already exists.",
        "يوجد قسم بهذا الاسم بالفعل.");

    public static string NotFound => LocalizedText.Get(
        "The department was not found.",
        "القسم غير موجود.");

    public static string Unavailable => LocalizedText.Get(
        "Choose an active department.",
        "اختر قسماً نشطاً.");

    public static string NotYours => LocalizedText.Get(
        "Choose one of your own departments.",
        "اختر أحد أقسامك.");

    public static string UnknownDepartments => LocalizedText.Get(
        "One or more departments do not exist.",
        "قسم واحد أو أكثر غير موجود.");
}
