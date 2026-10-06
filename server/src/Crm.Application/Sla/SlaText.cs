using Crm.Application.Common.Localization;

namespace Crm.Application.Sla;

/// <summary>User-facing text of the SLA feature, in the request language.</summary>
public static class SlaText
{
    public static string ResponseField => LocalizedText.Get("Response time", "وقت الاستجابة");

    public static string ResolutionField => LocalizedText.Get("Resolution time", "وقت الحل");

    public static string ResolutionBelowResponse => LocalizedText.Get(
        "The resolution time must be at least the response time.",
        "يجب ألا يقل وقت الحل عن وقت الاستجابة.");

    public static string PolicyNotFound => LocalizedText.Get(
        "There is no SLA policy for this priority.",
        "لا توجد سياسة SLA لهذه الأولوية.");
}
