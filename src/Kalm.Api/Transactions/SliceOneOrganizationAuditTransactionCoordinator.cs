using Kalm.Api.Configuration;
using Kalm.Audit.Application;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.Transactions;

/// <summary>
/// Coordinates the Slice 1 Organization and Audit writes on one local PostgreSQL transaction.
/// This composition-root type is intentionally narrow and is not a generic unit-of-work abstraction.
/// </summary>
public sealed class SliceOneOrganizationAuditTransactionCoordinator
{
    private readonly string _connectionString;

    public SliceOneOrganizationAuditTransactionCoordinator(IOptions<DatabaseOptions> databaseOptions)
    {
        _connectionString = databaseOptions.Value.ConnectionString;
    }

    public async Task ExecuteOrganizationAuditAsync(
        Func<OrganizationDbContext, IAuditWriter, AuditDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var organizationOptions = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(connection)
            .Options;
        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connection)
            .Options;

        await using var organizationContext = new OrganizationDbContext(organizationOptions);
        await using var auditContext = new AuditDbContext(auditOptions);

        await organizationContext.Database.UseTransactionAsync(transaction, cancellationToken);
        await auditContext.Database.UseTransactionAsync(transaction, cancellationToken);

        try
        {
            var auditWriter = new AuditWriter(auditContext);
            await operation(organizationContext, auditWriter, auditContext, cancellationToken);
            await organizationContext.SaveChangesAsync(cancellationToken);
            await auditContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
