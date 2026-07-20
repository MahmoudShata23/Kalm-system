using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection;

public static class OrganizationInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationInfrastructure(this IServiceCollection services, string connectionString)
        => services.AddOrganizationInfrastructure(_ => connectionString);

    public static IServiceCollection AddOrganizationInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        services.AddDbContext<OrganizationDbContext>((provider, options) =>
            options.UseNpgsql(connectionString(provider), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "organization")));
        return services;
    }
}
