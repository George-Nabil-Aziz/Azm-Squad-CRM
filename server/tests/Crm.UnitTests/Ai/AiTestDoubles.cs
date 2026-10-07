using Crm.Application.Ai;
using Crm.Domain.Ai;

namespace Crm.UnitTests.Ai;

/// <summary>An AI service that returns a canned answer (or throws) and remembers every request it got.</summary>
internal sealed class FakeAiTextService : IAiTextService
{
    public bool IsConfigured { get; set; } = true;

    public string Answer { get; set; } = "- Summary";

    public Exception? Failure { get; set; }

    public List<AiRequest> Requests { get; } = [];

    /// <summary>Answers by call number (the last one repeats) when set; overrides <see cref="Answer"/>.</summary>
    public List<string> Answers { get; } = [];

    public Task<string> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new AiNotConfiguredException();
        }

        Requests.Add(request);
        if (Failure is not null)
        {
            throw Failure;
        }

        var answer = Answers.Count == 0 ? Answer : Answers[Math.Min(Requests.Count, Answers.Count) - 1];
        return Task.FromResult(answer);
    }
}

internal sealed class FakeTicketSummaryRepository : ITicketSummaryRepository
{
    public List<TicketAiSummary> Rows { get; } = [];

    public int SaveCount { get; private set; }

    public Task<TicketAiSummary?> FindAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.TicketId == ticketId));

    public void Add(TicketAiSummary summary) => Rows.Add(summary);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
