using System.Reflection;
using Kalm.BuildingBlocks.Time;
using Kalm.Identity;
using Kalm.Organization;
using Kalm.Organization.Infrastructure;
using Kalm.Audit;
using Kalm.Audit.Infrastructure;
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

    [Fact]
    public void OrganizationAndAuditCore_DoNotDependOnApiOrPersistence()
    {
        string[] forbidden = ["Kalm.Api", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Npgsql"];
        AssertNoReferences(typeof(OrganizationAssemblyMarker).Assembly, forbidden);
        AssertNoReferences(typeof(AuditAssemblyMarker).Assembly, forbidden);
    }

    [Fact]
    public void ModuleInfrastructures_DoNotReferenceEachOther()
    {
        AssertNoReferences(typeof(OrganizationInfrastructureAssemblyMarker).Assembly, ["Kalm.Audit.Infrastructure"]);
        AssertNoReferences(typeof(AuditInfrastructureAssemblyMarker).Assembly, ["Kalm.Organization.Infrastructure"]);
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
