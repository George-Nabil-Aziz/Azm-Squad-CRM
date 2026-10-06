using System.Globalization;
using Crm.Application.Common.Localization;

namespace Crm.Application.KnowledgeBase;

/// <summary>User-facing text of the knowledge base feature, in the request language.</summary>
public static class KbText
{
    public static string TitleField => LocalizedText.Get("Title", "العنوان");

    public static string BodyField => LocalizedText.Get("Body", "المحتوى");

    public static string NameField => LocalizedText.Get("Name", "الاسم");

    public static string CategoryField => LocalizedText.Get("Category", "الفئة");

    public static string QuestionField => LocalizedText.Get("Question", "السؤال");

    public static string AnswerField => LocalizedText.Get("Answer", "الإجابة");

    public static string TitleRequired => LocalizedText.Get(
        "Enter a title (and a body) in English or Arabic.",
        "أدخل عنواناً ومحتوى بالإنجليزية أو بالعربية.");

    public static string BodyRequired => LocalizedText.Get(
        "Enter the body for this language, or remove its title.",
        "أدخل المحتوى لهذه اللغة أو احذف عنوانها.");

    public static string TitleRequiredForBody => LocalizedText.Get(
        "Enter the title for this language, or remove its body.",
        "أدخل العنوان لهذه اللغة أو احذف محتواها.");

    public static string NameRequired => LocalizedText.Get(
        "Enter a name in English or Arabic.",
        "أدخل اسماً بالإنجليزية أو بالعربية.");

    public static string CategoryNotFound => LocalizedText.Get(
        "Choose an existing category.",
        "اختر فئة موجودة.");

    public static string CategoryNotFoundResult => LocalizedText.Get(
        "The category was not found.",
        "الفئة غير موجودة.");

    public static string CategoryHasArticles => LocalizedText.Get(
        "The category still has articles. Move or delete them first.",
        "ما زالت الفئة تحتوي على مقالات. انقلها أو احذفها أولاً.");

    public static string ArticleNotFound => LocalizedText.Get(
        "The article was not found.",
        "المقال غير موجود.");

    public static string StatusInvalid => LocalizedText.Get(
        "Choose draft or published.",
        "اختر مسودة أو منشور.");

    public static string FaqNotFound => LocalizedText.Get(
        "The FAQ was not found.",
        "السؤال الشائع غير موجود.");

    public static string QuestionRequired => LocalizedText.Get(
        "Enter a question and an answer in English or Arabic.",
        "أدخل سؤالاً وإجابة بالإنجليزية أو بالعربية.");

    public static string AnswerRequired => LocalizedText.Get(
        "Enter the answer for this language, or remove its question.",
        "أدخل الإجابة لهذه اللغة أو احذف سؤالها.");

    public static string QuestionRequiredForAnswer => LocalizedText.Get(
        "Enter the question for this language, or remove its answer.",
        "أدخل السؤال لهذه اللغة أو احذف إجابتها.");

    public static string ArticleRequired => LocalizedText.Get(
        "Choose an article.",
        "اختر مقالاً.");

    public static string ArticleNotPublished => LocalizedText.Get(
        "Only a published article can be linked. Publish it first.",
        "لا يمكن إرفاق إلا مقال منشور. انشره أولاً.");

    public static string LanguageInvalid => LocalizedText.Get(
        "Choose en or ar.",
        "اختر en أو ar.");

    public static string DisplayOrderField => LocalizedText.Get("Display order", "ترتيب العرض");
}

/// <summary>
/// Picks the version of bilingual content for the request language (<see cref="CultureInfo.CurrentUICulture"/>),
/// falling back to the other language when the preferred version is missing.
/// </summary>
public static class KbLanguage
{
    public static bool IsArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == LocalizedText.Arabic;

    /// <summary>The Arabic text when the request language is Arabic and it exists, otherwise English, otherwise the other one.</summary>
    public static string Pick(string? en, string? ar)
    {
        var first = IsArabic ? ar : en;
        var second = IsArabic ? en : ar;
        return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
    }
}
