using System.Text.Json;
using Kalm.Api.Configuration;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Application.PinAuthentication;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.Transactions;

public sealed class PinAdministrationAuditTransactionCoordinator
{
    private readonly string _connectionString; private readonly IPinHasher _hasher; private readonly IClock _clock;
    public PinAdministrationAuditTransactionCoordinator(IOptions<DatabaseOptions> database, IPinHasher hasher, IClock clock) { _connectionString = database.Value.ConnectionString; _hasher = hasher; _clock = clock; }
    public async Task<PinAdministrationResult> SetAsync(Guid organizationId, Guid actorId, Guid userId, long expectedVersion, string pin, string correlationId, CancellationToken token)
    {
        string hash; try { hash = _hasher.Hash(pin); } catch (ArgumentException) { return PinAdministrationResult.Failure("user.pin_invalid"); }
        await using var connection = new NpgsqlConnection(_connectionString); await connection.OpenAsync(token); await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        await using var identity = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options); await using var auditContext = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options);
        await identity.Database.UseTransactionAsync(transaction, token); await auditContext.Database.UseTransactionAsync(transaction, token);
        try
        {
            User? user = await identity.Users.FromSqlInterpolated($"select * from identity.users where id = {userId} and organization_id = {organizationId} for update").SingleOrDefaultAsync(token);
            if (user is null) { await transaction.RollbackAsync(token); return PinAdministrationResult.Failure("user.not_found"); }
            if (user.Version != expectedVersion) { await transaction.RollbackAsync(token); return PinAdministrationResult.Failure("user.concurrency_conflict", user.Version); }
            if (user.Status == UserStatus.Archived) { await transaction.RollbackAsync(token); return PinAdministrationResult.Failure("user.archived"); }
            PinCredential? credential = await identity.PinCredentials.FromSqlInterpolated($"select * from identity.pin_credentials where user_id = {userId} for update").SingleOrDefaultAsync(token);
            bool reset = credential is not null; if (credential is null) identity.PinCredentials.Add(PinCredential.Create(Guid.NewGuid(), userId, hash, _clock.UtcNow)); else credential.Replace(hash, _clock.UtcNow);
            UserSession[] sessions = await identity.UserSessions.Where(session => session.UserId == userId && session.RevokedAtUtc == null).ToArrayAsync(token); foreach (UserSession session in sessions) session.Revoke(SessionRevocationReason.PinChanged, _clock.UtcNow);
            user.RecordCredentialChange(_clock.UtcNow);
            var writer = new AuditWriter(auditContext); await writer.AppendAsync(new AuditWriteRequest(Guid.NewGuid(), _clock.UtcNow, organizationId, null, null, actorId, AuditActorType.User, null, reset ? AuditAction.UserPinReset : AuditAction.UserPinSet, "User", userId, AuditResult.Succeeded, null, correlationId, null, JsonSerializer.Serialize(new { sessionsRevoked = sessions.Length }), null, null), token);
            await identity.SaveChangesAsync(token); await auditContext.SaveChangesAsync(token); await transaction.CommitAsync(token); return PinAdministrationResult.Success(user.Version);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
    }
}
public sealed record PinAdministrationResult(bool Succeeded, long Version, string? ErrorCode, long? CurrentVersion)
{
    public static PinAdministrationResult Success(long version) => new(true, version, null, null);
    public static PinAdministrationResult Failure(string code, long? current = null) => new(false, 0, code, current);
}
