using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kalm.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    private const string DevelopmentConnectionString = "Host=localhost;Port=54329;Database=kalm;Username=kalm;Password=kalm_dev_password";

    public IdentityDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("KALM_DATABASE_CONNECTION_STRING") ?? DevelopmentConnectionString;
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity"))
            .Options;
        return new IdentityDbContext(options);
    }
}
