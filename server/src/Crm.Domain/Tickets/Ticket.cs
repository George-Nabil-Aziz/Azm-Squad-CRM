using System.Globalization;

namespace Crm.Domain.Tickets;

/// <summary>
/// A customer request tracked until it is solved. <see cref="Number"/> is the sequential human number
/// ("TKT-000001"), given by the repository when the ticket is first saved. Tickets are never deleted (they are
/// closed). Times are UTC and come from the caller (the Application layer passes the injected TimeProvider's time).
/// </summary>
public sealed partial class Ticket
{
    public const int SubjectMaxLength = 200;
    public const int DescriptionMaxLength = 10_000;
    public const string NumberPrefix = "TKT-";

    private Ticket()
    {
        // EF Core materializes tickets through this constructor.
    }

    public Guid Id { get; private set; }

    /// <summary>Sequential number, unique over all tickets; 0 until the repository saves the ticket.</summary>
    public int Number { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public TicketStatus Status { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketChannel Channel { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>Optional category; may be deactivated later (the ticket keeps it).</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>The staff user working on the ticket (CRM-16); null while unassigned.</summary>
    public Guid? AssigneeId { get; private set; }

    /// <summary>The staff user who created the ticket; null for tickets that came in through a channel.</summary>
    public Guid? CreatedById { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>A closed ticket takes no replies or notes (until it is reopened).</summary>
    public bool AcceptsMessages => Status != TicketStatus.Closed;

    /// <summary>The prefix of this ticket's number, the one configured when it was created (CRM-35); "TKT-" by default.</summary>
    public string Prefix { get; private set; } = NumberPrefix;

    /// <summary>"TKT-000001" (with the ticket's own prefix).</summary>
    public string DisplayNumber => FormatNumber(Number, Prefix);

    /// <summary>A new ticket in status New. Subject is required and trimmed; a blank description becomes null.</summary>
    public static Ticket Create(
        Guid customerId,
        string subject,
        string? description,
        Guid? categoryId,
        TicketPriority priority,
        TicketChannel channel,
        Guid? createdById,
        DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A ticket needs a customer.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return new Ticket
        {
            Id = Guid.NewGuid(),
            Subject = subject.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status = TicketStatus.New,
            Priority = priority,
            Channel = channel,
            CustomerId = customerId,
            CategoryId = categoryId,
            CreatedById = createdById,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    /// <summary>Sets the sequential number (the repository calls it while saving a new ticket, again after a collision).</summary>
    public void AssignNumber(int number, string? prefix = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        Number = number;
        if (prefix is not null)
        {
            Prefix = prefix;
        }
    }

    /// <summary>An agent sent a public reply: the first one sets <see cref="FirstResponseAt"/>; every one bumps <see cref="UpdatedAt"/>.</summary>
    public void RecordAgentReply(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        MarkFirstResponse(utcNow); // the one place that sets FirstResponseAt (Ticket.Sla.cs), which SLA reads
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Moves the ticket along the workflow (<see cref="TicketStatusRules"/>). Becoming Resolved sets <see cref="ResolvedAt"/>;
    /// reopening (Resolved / Closed → Open) clears it. Throws <see cref="InvalidTicketStatusTransitionException"/> for an illegal move.
    /// </summary>
    public void ChangeStatus(TicketStatus to, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        if (!TicketStatusRules.CanMove(Status, to))
        {
            throw new InvalidTicketStatusTransitionException(Status, to);
        }

        if (to == TicketStatus.Resolved)
        {
            MarkResolved(utcNow);
        }
        else if (TicketStatusRules.IsReopen(Status, to))
        {
            Reopen();
        }

        Status = to;
        UpdatedAt = utcNow;
    }

    /// <summary>Sets the category (null = none). Returns false, changing nothing, when it already is that one.</summary>
    public bool ChangeCategory(Guid? categoryId, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        if (CategoryId == categoryId)
        {
            return false;
        }

        CategoryId = categoryId;
        UpdatedAt = utcNow;
        return true;
    }

    /// <summary>Gives the ticket to a staff user (null = unassigned). Returns false, changing nothing, when it already is theirs.</summary>
    public bool AssignTo(Guid? assigneeId, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        if (AssigneeId == assigneeId)
        {
            return false;
        }

        AssigneeId = assigneeId;
        UpdatedAt = utcNow;
        return true;
    }

    /// <summary>The customer wrote on the ticket through a channel: bumps <see cref="UpdatedAt"/> (never <see cref="FirstResponseAt"/>).</summary>
    public void RecordCustomerMessage(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        if (utcNow > UpdatedAt)
        {
            UpdatedAt = utcNow;
        }
    }

    /// <summary>1 → "TKT-000001" (at least six digits).</summary>
    public static string FormatNumber(int number) => FormatNumber(number, NumberPrefix);

    /// <summary>1 → "SUP-000001" with the given prefix.</summary>
    public static string FormatNumber(int number, string prefix) =>
        prefix + number.ToString("D6", CultureInfo.InvariantCulture);

    public const int PrefixMaxLength = 10;

    /// <summary>1–10 letters or digits (A-Z, 0-9) with an optional single trailing dash: "SUP", "SUP-", "A1-".</summary>
    public static bool IsValidPrefix(string? prefix)
    {
        var letters = prefix?.EndsWith('-') == true ? prefix[..^1] : prefix;
        return !string.IsNullOrEmpty(letters)
               && letters.Length <= PrefixMaxLength
               && letters.All(char.IsAsciiLetterOrDigit);
    }

    /// <summary>"sup" or " SUP- " → "SUP-" (upper case, always ending with a dash). The prefix must be valid.</summary>
    public static string NormalizePrefix(string prefix)
    {
        var trimmed = prefix.Trim().ToUpperInvariant();
        return trimmed.EndsWith('-') ? trimmed : trimmed + "-";
    }

    /// <summary>
    /// "TKT-000012", "SUP-12" (any prefix of 1–10 letters/digits), "tkt-12", "000012" or "12" → 12.
    /// Anything else (or 0) → false.
    /// </summary>
    public static bool TryParseNumber(string? text, out int number)
    {
        number = 0;
        var value = text?.Trim() ?? string.Empty;
        var dash = value.LastIndexOf('-');
        if (dash >= 0)
        {
            if (dash == 0 || !IsValidPrefix(value[..dash]) || value[dash - 1] == '-')
            {
                return false;
            }

            value = value[(dash + 1)..];
        }

        return value.Length > 0
               && value.All(char.IsAsciiDigit)
               && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number)
               && number > 0;
    }
}
