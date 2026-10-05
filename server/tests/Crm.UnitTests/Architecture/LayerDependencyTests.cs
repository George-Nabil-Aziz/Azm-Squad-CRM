namespace Crm.UnitTests.Architecture;

public class LayerDependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
        ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Crm.Infrastructure", "Crm.Api"];

    [Fact]
    public void Domain_DoesNotReferenceFrameworkOrOuterLayers() =>
        AssertNoForbiddenReferences(typeof(Crm.Domain.AssemblyReference).Assembly);

    [Fact]
    public void Application_DoesNotReferenceAspNetCoreOrOuterLayers() =>
        AssertNoForbiddenReferences(typeof(Crm.Application.AssemblyReference).Assembly);

    private static void AssertNoForbiddenReferences(System.Reflection.Assembly assembly)
    {
        var offending = assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => ForbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(offending);
    }
}
