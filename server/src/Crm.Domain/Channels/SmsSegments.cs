namespace Crm.Domain.Channels;

/// <summary>How a text is sent as SMS: <c>Encoding</c> "gsm7" or "ucs2" (any other character, e.g. Arabic or emoji).</summary>
public sealed record SmsSegmentInfo(string Encoding, int Units, int Segments, int SingleLimit, int MultiLimit);

/// <summary>
/// Counts SMS segments. GSM-7 text: 160 characters in one SMS, 153 per part when longer (a few characters such as € { } [ ]
/// count twice). Any character outside GSM-7 makes the whole text UCS-2: 70 in one SMS, 67 per part when longer.
/// </summary>
public static class SmsSegments
{
    private const string Gsm7Basic =
        "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

    private const string Gsm7Extension = "^{}\\[~]|€\f";

    public static SmsSegmentInfo Analyze(string? text)
    {
        text ??= string.Empty;
        var gsm7Units = 0;
        var isGsm7 = true;
        foreach (var character in text)
        {
            if (Gsm7Basic.Contains(character))
            {
                gsm7Units++;
            }
            else if (Gsm7Extension.Contains(character))
            {
                gsm7Units += 2;
            }
            else
            {
                isGsm7 = false;
                break;
            }
        }

        var (encoding, units, single, multi) = isGsm7 ? ("gsm7", gsm7Units, 160, 153) : ("ucs2", text.Length, 70, 67);
        var segments = units == 0 ? 0 : units <= single ? 1 : (units + multi - 1) / multi;
        return new SmsSegmentInfo(encoding, units, segments, single, multi);
    }
}
