using Crm.Application.Audit;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Application.Sla;
using Crm.Application.Tickets;
using Crm.Domain.Audit;
using Crm.Domain.Departments;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Departments;

/// <summary>Department use cases: validation, unique names, SLA overrides, the clock, storage through <see cref="IDepartmentRepository"/>.</summary>
public sealed class DepartmentService(
    IDepartmentRepository departments,
    TimeProvider timeProvider,
    IValidator<DepartmentRequest> requestValidator,
    IValidator<UpdateSlaPolicyRequest> slaValidator,
    IAuditLogger audit) : IDepartmentService
{
    public async Task<IReadOnlyList<DepartmentResponse>> ListAsync(ListDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var list = await departments.ListAsync(query.ActiveOnly == true, cancellationToken);
        return [.. list.Select(ToResponse)];
    }

    public async Task<DepartmentResponse> CreateAsync(DepartmentRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        await EnsureNameIsFreeAsync(request.Name!, null, cancellationToken);

        var now = UtcNow();
        var department = Department.Create(request.Name!, now);
        if (request.IsActive == false)
        {
            department.Update(request.Name!, isActive: false, now);
        }

        departments.Add(department);
        await departments.SaveChangesAsync(cancellationToken);
        return ToResponse(department);
    }

    public async Task<DepartmentResponse> UpdateAsync(Guid id, DepartmentRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var department = await departments.FindAsync(id, cancellationToken) ?? throw new NotFoundException(DepartmentText.NotFound);
        await EnsureNameIsFreeAsync(request.Name!, id, cancellationToken);

        department.Update(request.Name!, request.IsActive ?? department.IsActive, UtcNow());
        await departments.SaveChangesAsync(cancellationToken);
        return ToResponse(department);
    }

    public async Task<IReadOnlyList<DepartmentSlaPolicyResponse>> ListSlaPoliciesAsync(
        Guid departmentId, CancellationToken cancellationToken)
    {
        await RequireDepartmentAsync(departmentId, cancellationToken);
        var list = await departments.ListSlaPoliciesAsync(departmentId, cancellationToken);
        return [.. list.Select(ToResponse)];
    }

    public async Task<DepartmentSlaPolicyResponse> SetSlaPolicyAsync(
        Guid departmentId, string priority, UpdateSlaPolicyRequest request, CancellationToken cancellationToken)
    {
        await RequireDepartmentAsync(departmentId, cancellationToken);
        var parsed = ParsePriority(priority);
        await slaValidator.ValidateOrThrowAsync(request, cancellationToken);

        var now = UtcNow();
        var existing = await departments.FindSlaPolicyAsync(departmentId, parsed, cancellationToken);
        object? oldValues = existing is null ? null : new { existing.ResponseMinutes, existing.ResolutionMinutes };
        var policy = existing;
        if (policy is null)
        {
            policy = DepartmentSlaPolicy.Create(departmentId, parsed, request.ResponseMinutes!.Value, request.ResolutionMinutes!.Value, now);
            departments.AddSlaPolicy(policy);
        }
        else
        {
            policy.Update(request.ResponseMinutes!.Value, request.ResolutionMinutes!.Value, now);
        }

        await departments.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(
            new AuditEvent(AuditActions.SlaPolicyUpdated, "DepartmentSlaPolicy", $"{departmentId}/{TicketValues.PriorityName(parsed)}",
                oldValues, new { policy.ResponseMinutes, policy.ResolutionMinutes }),
            cancellationToken);
        return ToResponse(policy);
    }

    public async Task RemoveSlaPolicyAsync(Guid departmentId, string priority, CancellationToken cancellationToken)
    {
        await RequireDepartmentAsync(departmentId, cancellationToken);
        var parsed = ParsePriority(priority);
        if (await departments.FindSlaPolicyAsync(departmentId, parsed, cancellationToken) is not { } policy)
        {
            return;
        }

        departments.RemoveSlaPolicy(policy);
        await departments.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(
            new AuditEvent(AuditActions.SlaPolicyUpdated, "DepartmentSlaPolicy", $"{departmentId}/{TicketValues.PriorityName(parsed)}",
                new { policy.ResponseMinutes, policy.ResolutionMinutes }, null),
            cancellationToken);
    }

    private async Task RequireDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        if (await departments.FindAsync(departmentId, cancellationToken) is null)
        {
            throw new NotFoundException(DepartmentText.NotFound);
        }
    }

    private static TicketPriority ParsePriority(string priority) =>
        TicketValues.TryParsePriority(priority, out var parsed) ? parsed : throw new NotFoundException(SlaText.PolicyNotFound);

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await departments.NameExistsAsync(Department.NormalizeName(name), exceptId, cancellationToken))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["name"] = [DepartmentText.NameTaken] });
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static DepartmentResponse ToResponse(Department department) =>
        new(department.Id, department.Name, department.IsActive, department.CreatedAt, department.UpdatedAt);

    private static DepartmentSlaPolicyResponse ToResponse(DepartmentSlaPolicy policy) =>
        new(policy.DepartmentId, TicketValues.PriorityName(policy.Priority), policy.ResponseMinutes, policy.ResolutionMinutes, policy.UpdatedAt);
}
