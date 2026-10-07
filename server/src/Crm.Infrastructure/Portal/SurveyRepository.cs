using Crm.Application.Portal;
using Crm.Application.Reports;
using Crm.Domain.Portal;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Portal;

/// <summary>EF Core storage of satisfaction surveys.</summary>
public sealed class SurveyRepository(CrmDbContext db) : ISurveyRepository
{
    public Task<TicketSurvey?> FindByTokenAsync(string token, CancellationToken cancellationToken) =>
        db.TicketSurveys.FirstOrDefaultAsync(s => s.Token == token, cancellationToken);

    public Task<TicketSurvey?> FindByTicketAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.TicketSurveys.FirstOrDefaultAsync(s => s.TicketId == ticketId, cancellationToken);

    public void Add(TicketSurvey survey) => db.TicketSurveys.Add(survey);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// The CSAT report's read side (CRM-45..49) over the ratings of <c>TicketSurveys</c>: ratings given in the range with the
/// ticket's agent and category, and how many surveys were sent in it.
/// </summary>
public sealed class CsatReadModel(CrmDbContext db) : ICsatReadModel
{
    public async Task<CsatSnapshot> GetAsync(CsatFilter filter, CancellationToken cancellationToken)
    {
        var rows = await (
            from survey in db.TicketSurveys.AsNoTracking()
            where survey.Rating != null && survey.RatedAt >= filter.FromUtc && survey.RatedAt < filter.ToUtcExclusive
            join ticket in db.Tickets.AsNoTracking() on survey.TicketId equals ticket.Id
            where filter.BranchId == null || ticket.BranchId == filter.BranchId
            join agent in db.Users on ticket.AssigneeId equals agent.Id into agents
            from agent in agents.DefaultIfEmpty()
            join category in db.TicketCategories on ticket.CategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            orderby survey.RatedAt
            select new
            {
                ticket.Id,
                ticket.Number,
                ticket.Prefix,
                Rating = survey.Rating!.Value,
                survey.Comment,
                RatedAt = survey.RatedAt!.Value,
                AgentId = ticket.AssigneeId,
                AgentName = agent != null ? agent.FullName : null,
                CategoryId = ticket.CategoryId,
                CategoryName = category != null ? category.Name : null,
            }).ToListAsync(cancellationToken);

        var sent = await db.TicketSurveys.AsNoTracking()
            .Where(s => filter.BranchId == null || db.Tickets.Any(t => t.Id == s.TicketId && t.BranchId == filter.BranchId))
            .CountAsync(s => s.IssuedAt >= filter.FromUtc && s.IssuedAt < filter.ToUtcExclusive, cancellationToken);

        return new CsatSnapshot(
            [.. rows.Select(r => new CsatRating(
                r.Id, Crm.Domain.Tickets.Ticket.FormatNumber(r.Number, r.Prefix),
                r.Rating, r.Comment, r.RatedAt, r.AgentId, r.AgentName, r.CategoryId, r.CategoryName))],
            sent);
    }
}
