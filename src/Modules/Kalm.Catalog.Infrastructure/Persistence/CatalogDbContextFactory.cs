using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kalm.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    private const string DevelopmentConnectionString = "Host=localhost;Port=54329;Database=kalm;Username=kalm;Password=kalm_dev_password";

    public CatalogDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("KALM_DATABASE_CONNECTION_STRING") ?? DevelopmentConnectionString;
        return new CatalogDbContext(
            new DbContextOptionsBuilder<CatalogDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "catalog"))
                .Options);
    }
}
