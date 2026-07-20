using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kalm.Audit.Infrastructure.Persistence;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    private const string DevelopmentConnectionString = "Host=localhost;Port=54329;Database=kalm;Username=kalm;Password=kalm_dev_password";

    public AuditDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("KALM_DATABASE_CONNECTION_STRING") ?? DevelopmentConnectionString;
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit"))
            .Options;
        return new AuditDbContext(options);
    }
}
