using Kalm.Api.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kalm.Api.Features.Health;

public sealed class PostgreSqlReadinessCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PostgreSqlReadinessCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        KalmDbContext dbContext = scope.ServiceProvider.GetRequiredService<KalmDbContext>();

        bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
            : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
    }
}
