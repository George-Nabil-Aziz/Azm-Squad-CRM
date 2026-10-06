namespace Crm.Api.IntegrationTests.Sla;

/// <summary>JSON shape of /api/sla-policies responses, as the client sees them.</summary>
public sealed record SlaPolicyBody(string Priority, int ResponseMinutes, int ResolutionMinutes, DateTime UpdatedAt);
