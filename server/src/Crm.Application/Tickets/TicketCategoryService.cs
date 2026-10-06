using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Tickets;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Tickets;

/// <summary>Ticket category use cases: validation, unique names, the clock, storage through <see cref="ITicketCategoryRepository"/>.</summary>
public sealed class TicketCategoryService(
    ITicketCategoryRepository categories,
    TimeProvider timeProvider,
    IValidator<TicketCategoryRequest> requestValidator) : ITicketCategoryService
{
    public async Task<IReadOnlyList<TicketCategoryResponse>> ListAsync(
        ListTicketCategoriesQuery query, CancellationToken cancellationToken)
    {
        var list = await categories.ListAsync(query.ActiveOnly == true, cancellationToken);
        return [.. list.Select(ToResponse)];
    }

    public async Task<TicketCategoryResponse> CreateAsync(TicketCategoryRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        await EnsureNameIsFreeAsync(request.Name!, null, cancellationToken);

        var now = UtcNow();
        var category = TicketCategory.Create(request.Name!, now);
        if (request.IsActive == false)
        {
            category.Update(request.Name!, isActive: false, now);
        }

        categories.Add(category);
        await categories.SaveChangesAsync(cancellationToken);
        return ToResponse(category);
    }

    public async Task<TicketCategoryResponse> UpdateAsync(
        Guid id, TicketCategoryRequest request, CancellationToken cancellationToken)
    {
        await requestValidator.ValidateOrThrowAsync(request, cancellationToken);
        var category = await categories.FindAsync(id, cancellationToken)
                       ?? throw new NotFoundException(TicketCategoryText.NotFound);
        await EnsureNameIsFreeAsync(request.Name!, id, cancellationToken);

        category.Update(request.Name!, request.IsActive ?? category.IsActive, UtcNow());
        await categories.SaveChangesAsync(cancellationToken);
        return ToResponse(category);
    }

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await categories.NameExistsAsync(TicketCategory.NormalizeName(name), exceptId, cancellationToken))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["name"] = [TicketCategoryText.NameTaken] });
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static TicketCategoryResponse ToResponse(TicketCategory category) =>
        new(category.Id, category.Name, category.IsActive, category.CreatedAt, category.UpdatedAt);
}
