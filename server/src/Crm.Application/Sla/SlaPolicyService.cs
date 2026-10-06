using Crm.Application.Audit;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Application.Tickets;
using Crm.Domain.Audit;
using Crm.Domain.Sla;
using FluentValidation;

namespace Crm.Application.Sla;

/// <summary>SLA policy use cases: priority parsing, validation, the clock, storage through <see cref="ISlaPolicyRepository"/>.</summary>
public sealed class SlaPolicyService(
    ISlaPolicyRepository policies,
    TimeProvider timeProvider,
    IValidator<UpdateSlaPolicyRequest> requestValidator,
    IAuditLogger audit) : ISlaPolicyService
{
    public async Task<IReadOnlyList<SlaPolicyResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var list = await policies.ListAsync(cancellationToken);
        return [.. list.Select(ToResponse)];
    }

    public async Task<SlaPolicyResponse> UpdateAsync(
        string priority, UpdateSlaPolicyRequest request, CancellationToken cancellationToken)
    {
        if (!TicketValues.TryParsePriority(priority, out var parsed))
        {
            throw new NotFoundException(SlaText.PolicyNotFound);
        }

        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var policy = await policies.FindAsync(parsed, cancellationToken)
                     ?? throw new NotFoundException(SlaText.PolicyNotFound);

        var oldValues = new { responseMinutes = policy.ResponseMinutes, resolutionMinutes = policy.ResolutionMinutes };
        policy.Update(request.ResponseMinutes!.Value, request.ResolutionMinutes!.Value, timeProvider.GetUtcNow().UtcDateTime);
        await policies.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(
            new AuditEvent(AuditActions.SlaPolicyUpdated, "SlaPolicy", TicketValues.PriorityName(parsed), oldValues,
                new { responseMinutes = policy.ResponseMinutes, resolutionMinutes = policy.ResolutionMinutes }),
            cancellationToken);
        return ToResponse(policy);
    }

    private static SlaPolicyResponse ToResponse(SlaPolicy policy) =>
        new(TicketValues.PriorityName(policy.Priority), policy.ResponseMinutes, policy.ResolutionMinutes, policy.UpdatedAt);
}
