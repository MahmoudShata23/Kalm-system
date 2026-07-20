using Kalm.Audit.Application;
using Kalm.Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection;

public static class AuditInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, string connectionString)
        => services.AddAuditInfrastructure(_ => connectionString);

    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        services.AddDbContext<AuditDbContext>((provider, options) =>
            options.UseNpgsql(connectionString(provider), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit")));
        services.AddScoped<IAuditWriter, AuditWriter>();
        return services;
    }
}
