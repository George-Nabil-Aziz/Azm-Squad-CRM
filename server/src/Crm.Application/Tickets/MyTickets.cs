using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Domain.Tickets;
using FluentValidation;

namespace Crm.Application.Tickets;

/// <summary>GET /api/tickets/mine query: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).</summary>
public sealed record MyTicketsQuery(int? Page, int? PageSize);

/// <summary>Counters of the agent dashboard: <c>Open</c> = New or Open, <c>BreachedToday</c> = SLA breaches of the current UTC day.</summary>
public sealed record MyTicketCounters(int Open, int Pending, int BreachedToday);

/// <summary>The tickets assigned to me that are not Closed, nearest SLA due time first, with the counters.</summary>
public sealed record MyTicketsResponse(MyTicketCounters Counters, PagedResult<TicketResponse> Tickets);

/// <summary>Storage of the agent dashboard (implemented in Crm.Infrastructure with EF Core).</summary>
public interface IMyTicketsRepository
{
    /// <summary>Every ticket assigned to the user that is not Closed (customer, category and assignee names included), unsorted.</summary>
    Task<IReadOnlyList<TicketView>> ListOpenAssignedAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user's non-Closed tickets that have a response or resolution breach event in [dayStartUtc, dayEndUtc);
    /// every ticket counts once.
    /// </summary>
    Task<int> CountBreachedTodayAsync(Guid userId, DateTime dayStartUtc, DateTime dayEndUtc, CancellationToken cancellationToken);
}

/// <summary>The agent dashboard (CRM-29). <c>ValidationException</c> 400 for bad paging.</summary>
public interface IMyTicketsService
{
    Task<MyTicketsResponse> GetAsync(MyTicketsQuery query, CancellationToken cancellationToken);
}

public sealed class MyTicketsQueryValidator : AbstractValidator<MyTicketsQuery>
{
    public MyTicketsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(PagingDefaults.DefaultPage).WithName(_ => PagingText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => PagingText.PageSizeField);
    }
}

public sealed class MyTicketsService(
    IMyTicketsRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<MyTicketsQuery> validator) : IMyTicketsService
{
    public async Task<MyTicketsResponse> GetAsync(MyTicketsQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(query, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedException(TicketText.NotFound);
        var views = await repository.ListOpenAssignedAsync(userId, cancellationToken);

        // AC 2: nearest SLA due first; tickets without a due time (no policy, resolved) last, oldest first.
        var sorted = views
            .OrderBy(v => v.Ticket.NextSlaDueAt is null)
            .ThenBy(v => v.Ticket.NextSlaDueAt)
            .ThenBy(v => v.Ticket.CreatedAt)
            .ThenBy(v => v.Ticket.Number)
            .ToList();

        var today = timeProvider.GetUtcNow().UtcDateTime.Date;
        var dayStart = DateTime.SpecifyKind(today, DateTimeKind.Utc);
        var counters = new MyTicketCounters(
            sorted.Count(v => v.Ticket.Status is TicketStatus.New or TicketStatus.Open),
            sorted.Count(v => v.Ticket.Status == TicketStatus.Pending),
            await repository.CountBreachedTodayAsync(userId, dayStart, dayStart.AddDays(1), cancellationToken));

        var page = query.Page ?? PagingDefaults.DefaultPage;
        var pageSize = query.PageSize ?? PagingDefaults.DefaultPageSize;
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).Select(TicketService.ToResponse).ToList();
        return new MyTicketsResponse(counters, new PagedResult<TicketResponse>(items, page, pageSize, sorted.Count));
    }
}
