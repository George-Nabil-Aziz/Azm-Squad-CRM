using Crm.Domain.Customers;

namespace Crm.Application.Customers.Timeline;

/// <summary>API names of <see cref="InteractionType"/>: "customer", "note", "attachment", "ticket", "message".</summary>
public static class InteractionTypes
{
    private static readonly Dictionary<string, InteractionType> ByName = new(StringComparer.Ordinal)
    {
        ["customer"] = InteractionType.Customer,
        ["note"] = InteractionType.Note,
        ["attachment"] = InteractionType.Attachment,
        ["ticket"] = InteractionType.Ticket,
        ["message"] = InteractionType.Message,
    };

    /// <summary>Parses an API name (lower case only; numbers and other spellings are rejected).</summary>
    public static bool TryParse(string? name, out InteractionType type) =>
        ByName.TryGetValue(name ?? string.Empty, out type);

    public static string Name(InteractionType type) => ByName.First(pair => pair.Value == type).Key;
}
