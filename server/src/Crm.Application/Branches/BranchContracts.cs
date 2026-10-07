using Crm.Domain.Branches;
using FluentValidation;

namespace Crm.Application.Branches;

/// <summary>GET /api/branches query string: <c>activeOnly=true</c> returns only the branches that can be chosen.</summary>
public sealed record ListBranchesQuery(bool? ActiveOnly);

/// <summary>Body of POST / PUT /api/branches. <c>IsActive</c>: null = active on create, unchanged on update.</summary>
public sealed record BranchRequest(string? Name, bool? IsActive);

/// <summary>A branch as the API returns it (times UTC).</summary>
public sealed record BranchResponse(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>Body of PUT /api/users/{id}/branch: the user's branch; null = no branch (sees every branch).</summary>
public sealed record SetUserBranchRequest(Guid? BranchId);

/// <summary>Branch storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface IBranchRepository
{
    Task<IReadOnlyList<Branch>> ListAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<Branch?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    void Add(Branch branch);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Branches (reads: any signed-in staff user; writes <c>branches.manage</c>, enforced by the API).</summary>
public interface IBranchService
{
    /// <summary>Branches by name; a branch-restricted user only gets their own.</summary>
    Task<IReadOnlyList<BranchResponse>> ListAsync(ListBranchesQuery query, CancellationToken cancellationToken);

    Task<BranchResponse> CreateAsync(BranchRequest request, CancellationToken cancellationToken);

    Task<BranchResponse> UpdateAsync(Guid id, BranchRequest request, CancellationToken cancellationToken);
}

public sealed class BranchRequestValidator : AbstractValidator<BranchRequest>
{
    public BranchRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Branch.NameMaxLength).WithName(_ => BranchText.NameField);
    }
}
