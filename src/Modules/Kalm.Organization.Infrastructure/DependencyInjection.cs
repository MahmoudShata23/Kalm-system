using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection;

public static class OrganizationInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OrganizationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "organization")));
        return services;
    }
}
