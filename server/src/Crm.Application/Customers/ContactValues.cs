using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Crm.Domain.Customers;
using PhoneNumbers;

namespace Crm.Application.Customers;

/// <summary>
/// Converts contact values typed by agents or sent by channels into their stored form, and maps contact types to their
/// API names. Phone numbers become E.164 ("+966501234567") with libphonenumber; numbers without a country code are
/// read as numbers of <see cref="DefaultRegion"/>.
/// </summary>
public static partial class ContactValues
{
    /// <summary>Region of numbers typed without a country code ("0501234567" → "+966501234567").</summary>
    public const string DefaultRegion = "SA";

    /// <summary>Longest phone number input accepted (spaces and brackets included).</summary>
    public const int PhoneInputMaxLength = 32;

    private static readonly PhoneNumberUtil Numbers = PhoneNumberUtil.GetInstance();

    private static readonly Dictionary<string, ContactType> TypesByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["phone"] = ContactType.Phone,
        ["email"] = ContactType.Email,
        ["whatsapp"] = ContactType.WhatsApp,
    };

    /// <summary>
    /// True when <paramref name="input"/> is a real phone number (digits in any script, optional leading "+", spaces,
    /// dashes, dots, brackets; no letters, no extension); <paramref name="e164"/> is then its E.164 form.
    /// </summary>
    public static bool TryNormalizePhone(string? input, [NotNullWhen(true)] out string? e164)
    {
        e164 = null;
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > PhoneInputMaxLength || !PhoneCharacters().IsMatch(value))
        {
            return false;
        }

        try
        {
            var number = Numbers.Parse(value, DefaultRegion);
            if (number.HasExtension || !Numbers.IsValidNumber(number))
            {
                return false;
            }

            e164 = Numbers.Format(number, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    /// <summary>"phone", "email" or "whatsapp" (any case) → the contact type. Numbers ("1") are not accepted.</summary>
    public static bool TryParseType(string? name, out ContactType type)
    {
        type = default;
        return name is not null && TypesByName.TryGetValue(name.Trim(), out type);
    }

    /// <summary>The API name of a contact type: "phone", "email" or "whatsapp".</summary>
    public static string TypeName(ContactType type) => TypesByName.Single(pair => pair.Value == type).Key;

    /// <summary>Optional leading "+", then digits (any script), spaces, dots, dashes and brackets.</summary>
    [GeneratedRegex(@"^\+?[\d\s().\-]+$")]
    private static partial Regex PhoneCharacters();
}
