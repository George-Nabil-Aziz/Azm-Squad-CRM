using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Domain.Branches;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Branches;

/// <summary>Branch use cases: validation, unique names, the clock, storage through <see cref="IBranchRepository"/>.</summary>
public sealed class BranchService(
    IBranchRepository branches,
    TimeProvider timeProvider,
    IValidator<BranchRequest> requestValidator,
    IDataScope? dataScope = null) : IBranchService
{
    public async Task<IReadOnlyList<BranchResponse>> ListAsync(ListBranchesQuery query, CancellationToken cancellationToken)
    {
        var list = await branches.ListAsync(query.ActiveOnly == true, cancellationToken);
        return [.. list.Where(b => dataScope is not { RestrictBranch: true } || b.Id == dataScope.BranchId).Select(ToResponse)];
    }

    public async Task<BranchResponse> CreateAsync(BranchRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        await EnsureNameIsFreeAsync(request.Name!, null, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var branch = Branch.Create(request.Name!, now);
        if (request.IsActive == false)
        {
            branch.Update(request.Name!, isActive: false, now);
        }

        branches.Add(branch);
        await branches.SaveChangesAsync(cancellationToken);
        return ToResponse(branch);
    }

    public async Task<BranchResponse> UpdateAsync(Guid id, BranchRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var branch = await branches.FindAsync(id, cancellationToken) ?? throw new NotFoundException(BranchText.NotFound);
        await EnsureNameIsFreeAsync(request.Name!, id, cancellationToken);

        branch.Update(request.Name!, request.IsActive ?? branch.IsActive, timeProvider.GetUtcNow().UtcDateTime);
        await branches.SaveChangesAsync(cancellationToken);
        return ToResponse(branch);
    }

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await branches.NameExistsAsync(Branch.NormalizeName(name), exceptId, cancellationToken))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["name"] = [BranchText.NameTaken] });
        }
    }

    private static BranchResponse ToResponse(Branch branch) =>
        new(branch.Id, branch.Name, branch.IsActive, branch.CreatedAt, branch.UpdatedAt);
}
