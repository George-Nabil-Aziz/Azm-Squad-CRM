namespace Crm.Api.IntegrationTests.Sla;

/// <summary>JSON shape of /api/sla-policies responses, as the client sees them.</summary>
public sealed record SlaPolicyBody(string Priority, int ResponseMinutes, int ResolutionMinutes, DateTime UpdatedAt);

/// <summary>The SLA part of a ticket (/api/tickets), as the client sees it.</summary>
public sealed record TicketSlaBody(
    Guid Id,
    string Priority,
    DateTime CreatedAt,
    DateTime? ResponseDueAt,
    DateTime? ResolutionDueAt,
    DateTime? FirstResponseAt,
    DateTime? ResolvedAt);
