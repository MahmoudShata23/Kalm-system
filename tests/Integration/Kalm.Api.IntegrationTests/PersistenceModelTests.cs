using Kalm.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void PlatformModel_UsesPostgreSqlSchemaAndRequiredTables()
    {
        var options = new DbContextOptionsBuilder<KalmDbContext>()
            .UseNpgsql("Host=localhost;Port=54329;Database=kalm;Username=kalm;Password=kalm_dev_password")
            .Options;

        using var context = new KalmDbContext(options);

        var tables = context.Model.GetEntityTypes()
            .Select(entity => $"{entity.GetSchema()}.{entity.GetTableName()}")
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            ["platform.idempotency_records", "platform.outbox_messages", "platform.schema_markers"],
            tables);
    }
}
