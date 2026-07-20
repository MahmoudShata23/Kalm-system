using Kalm.Audit.Application;
using Kalm.Audit.Domain;

namespace Kalm.UnitTests.Audit;

public sealed class AuditDomainTests
{
    [Fact]
    public void AuditEntry_RequiresUtcTimestampAndBoundedFields()
    {
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(Guid.NewGuid(), DateTimeOffset.Now, null, null, null, null, AuditActorType.System, null, AuditAction.OrganizationCreated, "Organization", Guid.NewGuid(), AuditResult.Succeeded, null, "correlation", null, null, null, null));
    }

    [Fact]
    public void RedactionPolicy_ProducesDeterministicJsonAndRejectsSecrets()
    {
        string? json = AuditRedactionPolicy.CreateJson(new Dictionary<string, string?>
        {
            ["status"] = "Setup",
            ["brandName"] = "Kalm"
        });

        Assert.Equal("{\"brandName\":\"Kalm\",\"status\":\"Setup\"}", json);
        Assert.Throws<ArgumentException>(() => AuditRedactionPolicy.CreateJson(new Dictionary<string, string?> { ["password"] = "secret" }));
    }
}
