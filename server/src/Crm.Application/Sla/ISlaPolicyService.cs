namespace Crm.Application.Sla;

/// <summary>
/// SLA policy per priority (CRM-19; the API requires <c>sla.manage</c> = SuperAdmin only).
/// Failures: <c>ValidationException</c> 400 (invalid times), <c>NotFoundException</c> 404 (unknown priority).
/// </summary>
public interface ISlaPolicyService
{
    /// <summary>The policies of High, Mid and Low, in that order.</summary>
    Task<IReadOnlyList<SlaPolicyResponse>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Sets the response and resolution minutes of one priority (API name, any case).</summary>
    Task<SlaPolicyResponse> UpdateAsync(string priority, UpdateSlaPolicyRequest request, CancellationToken cancellationToken);
}
