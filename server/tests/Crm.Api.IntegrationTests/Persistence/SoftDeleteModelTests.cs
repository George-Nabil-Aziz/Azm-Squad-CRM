using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Domain.Common;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Persistence;

/// <summary>Guard: every soft-deletable entity is hidden by the named "SoftDelete" query filter (CLAUDE.md).</summary>
public class SoftDeleteModelTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    /// <summary>Same value as CrmDbContext.SoftDeleteFilter (written out so this test fails, not breaks the build, without it).</summary>
    private const string SoftDeleteFilter = "SoftDelete";

    [Fact]
    public void EverySoftDeletableEntity_HasTheSoftDeleteQueryFilter()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<CrmDbContext>().Model;

        var softDeletable = model.GetEntityTypes()
            .Where(type => typeof(ISoftDeletable).IsAssignableFrom(type.ClrType))
            .ToList();

        Assert.Contains(softDeletable, type => type.ClrType == typeof(Customer));
        Assert.All(softDeletable, type => Assert.NotNull(type.FindDeclaredQueryFilter(SoftDeleteFilter)));
    }
}
