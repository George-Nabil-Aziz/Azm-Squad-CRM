using Crm.Application.Common.Localization;

namespace Crm.Application.Portal;

/// <summary>User-facing text of the satisfaction survey, in the request language.</summary>
public static class SurveyText
{
    public static string NotFound => LocalizedText.Get(
        "The survey was not found.",
        "الاستبيان غير موجود.");

    public static string NoSurvey => LocalizedText.Get(
        "There is no survey for this request yet. It opens when the request is resolved.",
        "لا يوجد استبيان لهذا الطلب بعد. يُفتح عند حل الطلب.");

    public static string RatingInvalid => LocalizedText.Get(
        "Choose a rating from 1 to 5.",
        "اختر تقييماً من 1 إلى 5.");

    public static string AlreadyRated => LocalizedText.Get(
        "You already rated this request.",
        "لقد قيّمت هذا الطلب بالفعل.");

    public static string Expired(int days) => LocalizedText.Get(
        $"This survey link expired (it is valid for {days} days).",
        $"انتهت صلاحية رابط الاستبيان (صالح لمدة {days} أيام).");

    public static string CommentTooLong(int max) => LocalizedText.Get(
        $"The comment can have at most {max} characters.",
        $"يمكن أن يحتوي التعليق على {max} حرف كحد أقصى.");

    public static string EmailSubject => LocalizedText.Get("How did we do?", "كيف كانت تجربتك معنا؟");

    public static string EmailBody(string number, string subject, string link, int days) => LocalizedText.Get(
        $"Your request \"{subject}\" ({number}) was resolved.\nPlease tell us how we did (1 to 5 stars):\n{link}\nThe link is valid for {days} days.",
        $"تم حل طلبك \"{subject}\" ({number}).\nيرجى تقييم الخدمة (من 1 إلى 5 نجوم):\n{link}\nالرابط صالح لمدة {days} أيام.");
}
