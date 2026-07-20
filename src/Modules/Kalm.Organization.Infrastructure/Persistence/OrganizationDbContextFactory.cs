using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kalm.Organization.Infrastructure.Persistence;

public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    private const string DevelopmentConnectionString = "Host=localhost;Port=54329;Database=kalm;Username=kalm;Password=kalm_dev_password";

    public OrganizationDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("KALM_DATABASE_CONNECTION_STRING") ?? DevelopmentConnectionString;
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "organization"))
            .Options;
        return new OrganizationDbContext(options);
    }
}
