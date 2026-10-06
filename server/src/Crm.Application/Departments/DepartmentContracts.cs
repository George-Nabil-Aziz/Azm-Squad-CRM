namespace Crm.Application.Departments;

/// <summary>GET /api/departments query string: <c>activeOnly=true</c> returns only the departments tickets may use.</summary>
public sealed record ListDepartmentsQuery(bool? ActiveOnly);

/// <summary>Body of POST / PUT /api/departments. <c>IsActive</c>: null = active on create, unchanged on update.</summary>
public sealed record DepartmentRequest(string? Name, bool? IsActive);

/// <summary>A department as the API returns it (times UTC).</summary>
public sealed record DepartmentResponse(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>A per-department SLA override (priority API name "high" / "mid" / "low").</summary>
public sealed record DepartmentSlaPolicyResponse(
    Guid DepartmentId, string Priority, int ResponseMinutes, int ResolutionMinutes, DateTime UpdatedAt);
