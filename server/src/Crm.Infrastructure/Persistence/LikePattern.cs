namespace Crm.Infrastructure.Persistence;

/// <summary>"Contains" patterns for <c>EF.Functions.Like</c> in which %, _ and \ typed by the user match literally.</summary>
internal static class LikePattern
{
    public const string EscapeCharacter = "\\";

    /// <summary>"50%_off" → "%50\%\_off%".</summary>
    public static string Contains(string text) =>
        "%" + text.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter)
            .Replace("%", EscapeCharacter + "%")
            .Replace("_", EscapeCharacter + "_") + "%";
}
