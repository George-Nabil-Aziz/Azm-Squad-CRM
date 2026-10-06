using System.Text.RegularExpressions;

namespace Crm.Domain.QuickReplies;

/// <summary>The ticket and agent data placeholders are replaced with.</summary>
public sealed record QuickReplyValues(string CustomerName, string TicketNumber, string TicketSubject, string AgentName);

/// <summary>
/// Placeholder rule of quick replies (CRM-32): <c>{{customer.name}}</c>, <c>{{ticket.number}}</c>, <c>{{ticket.subject}}</c> and
/// <c>{{agent.name}}</c>; spacing and letter case inside the braces are ignored, unknown placeholders stay as typed, and
/// replaced values are never expanded again.
/// </summary>
public static partial class QuickReplyTemplate
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z.]+)\s*\}\}")]
    private static partial Regex Placeholder();

    public static string Render(string body, QuickReplyValues values) =>
        Placeholder().Replace(body, match => match.Groups[1].Value.ToLowerInvariant() switch
        {
            "customer.name" => values.CustomerName,
            "ticket.number" => values.TicketNumber,
            "ticket.subject" => values.TicketSubject,
            "agent.name" => values.AgentName,
            _ => match.Value,
        });
}

/// <summary>A saved reply template: personal (only its owner sees it) or shared (everybody sees it).</summary>
public sealed class QuickReply
{
    public const int TitleMaxLength = 100;
    public const int ShortcutMaxLength = 50;
    public const int BodyMaxLength = 5000;

    private QuickReply()
    {
        // EF Core materializes quick replies through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Short text to find the reply by (e.g. "/thanks"); optional.</summary>
    public string? Shortcut { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public bool IsShared { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static QuickReply Create(Guid ownerId, string title, string? shortcut, string body, bool isShared, DateTime utcNow)
    {
        var reply = new QuickReply { Id = Guid.NewGuid(), OwnerId = ownerId, CreatedAt = utcNow };
        reply.Apply(title, shortcut, body, isShared, utcNow);
        return reply;
    }

    public void Update(string title, string? shortcut, string body, bool isShared, DateTime utcNow) =>
        Apply(title, shortcut, body, isShared, utcNow);

    private void Apply(string title, string? shortcut, string body, bool isShared, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        Title = title.Trim();
        Shortcut = string.IsNullOrWhiteSpace(shortcut) ? null : shortcut.Trim();
        Body = body.Trim();
        IsShared = isShared;
        UpdatedAt = utcNow;
    }
}
