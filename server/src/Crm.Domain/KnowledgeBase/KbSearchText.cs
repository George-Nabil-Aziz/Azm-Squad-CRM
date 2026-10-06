using System.Text;

namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// The text form used to match knowledge base searches (CRM-38): lower case, Arabic letter variants folded
/// (أ إ آ ٱ → ا, ة → ه, ى → ي), diacritics and tatweel removed, Arabic-Indic digits → ASCII, whitespace collapsed.
/// Stored text and the query go through the same function, so "إدارة" finds "اداره" and the other way round.
/// </summary>
public static class KbSearchText
{
    public const int MaxTerms = 8;
    public const int MaxTermLength = 50;

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var lastWasSpace = true;
        foreach (var character in text)
        {
            var mapped = Map(character);
            if (mapped is null)
            {
                continue; // diacritic / tatweel
            }

            if (char.IsWhiteSpace(mapped.Value))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            builder.Append(mapped.Value);
            lastWasSpace = false;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>The searchable text of an item: every given text, normalized, joined by a space.</summary>
    public static string Build(params string?[] texts) =>
        string.Join(' ', texts.Select(Normalize).Where(text => text.Length > 0));

    /// <summary>The distinct normalized words of a query (at most <see cref="MaxTerms"/>, each cut to <see cref="MaxTermLength"/>).</summary>
    public static IReadOnlyList<string> Terms(string? query) =>
        [.. Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Length > MaxTermLength ? term[..MaxTermLength] : term)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTerms)];

    private static char? Map(char character) => character switch
    {
        'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', // alef with hamza / madda / wasla -> alef
        'ة' => 'ه', // teh marbuta -> heh
        'ى' => 'ي', // alef maksura -> yeh
        'ـ' => null, // tatweel
        >= 'ً' and <= 'ٟ' => null, // Arabic diacritics (fathatan .. wavy hamza below)
        'ٰ' => null, // superscript alef
        >= '٠' and <= '٩' => (char)('0' + (character - '٠')), // Arabic-Indic digits
        _ => char.ToLowerInvariant(character),
    };
}
