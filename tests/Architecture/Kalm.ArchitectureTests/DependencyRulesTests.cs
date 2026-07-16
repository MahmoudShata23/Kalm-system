using System.Reflection;
using Kalm.BuildingBlocks.Time;
using Kalm.Identity;
using Kalm.SharedKernel.Errors;

namespace Kalm.ArchitectureTests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void SharedKernel_DoesNotDependOnFrameworkOrInfrastructureAssemblies()
    {
        var forbiddenPrefixes = new[]
        {
            "Kalm.Api",
            "Kalm.BuildingBlocks",
            "Kalm.Identity",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Npgsql"
        };

        AssertNoReferences(typeof(AppError).Assembly, forbiddenPrefixes);
    }

    [Fact]
    public void BuildingBlocks_DoesNotDependOnApiOrModules()
    {
        AssertNoReferences(typeof(SystemClock).Assembly, ["Kalm.Api", "Kalm.Identity"]);
    }

    [Fact]
    public void IdentityModule_DoesNotDependOnApiOrInfrastructure()
    {
        AssertNoReferences(
            typeof(IdentityAssemblyMarker).Assembly,
            ["Kalm.Api", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql"]);
    }

    private static void AssertNoReferences(Assembly assembly, IReadOnlyCollection<string> forbiddenPrefixes)
    {
        var violations = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .OrderBy(name => name)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"{assembly.GetName().Name} has forbidden references: {string.Join(", ", violations)}");
    }
}
