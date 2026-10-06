namespace Crm.Application.Sla;

/// <summary>Body of PUT /api/sla-policies/{priority}: whole minutes, both required, resolution &gt;= response.</summary>
public sealed record UpdateSlaPolicyRequest(int? ResponseMinutes, int? ResolutionMinutes);

/// <summary>An SLA policy as the API returns it: priority API name ("high" / "mid" / "low"), minutes, UTC time.</summary>
public sealed record SlaPolicyResponse(string Priority, int ResponseMinutes, int ResolutionMinutes, DateTime UpdatedAt);
