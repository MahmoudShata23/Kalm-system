using Kalm.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection;

public static class CatalogInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, string connectionString)
        => services.AddCatalogInfrastructure(_ => connectionString);

    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        services.AddDbContext<CatalogDbContext>((provider, options) =>
            options.UseNpgsql(connectionString(provider), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "catalog")));
        return services;
    }
}
