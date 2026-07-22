using Kalm.Api.Configuration;
using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;
using Kalm.Api.Features.DeviceAdministration;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Application.PinAuthentication;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.Transactions;

public sealed class DeviceAuthenticationAuditTransactionCoordinator
{
    private readonly string _connectionString; private readonly IPinHasher _pinHasher; private readonly DummyPinHash _dummy; private readonly IClock _clock; private readonly ManagementAuthenticationOptions _options;
    public DeviceAuthenticationAuditTransactionCoordinator(IOptions<DatabaseOptions> database, IPinHasher pinHasher, DummyPinHash dummy, IClock clock, IOptions<ManagementAuthenticationOptions> options)
    { _connectionString = database.Value.ConnectionString; _pinHasher = pinHasher; _dummy = dummy; _clock = clock; _options = options.Value; }

    public Task<DevicePinLoginResult> LoginAsync(DeviceRequestContext device, Guid requestedUserId, string pin, string correlationId, CancellationToken token)
        => ExecuteAsync(async (organization, identity, audit, now, ct) =>
        {
            string lockKey = $"pin-login:{device.DeviceId:N}:{requestedUserId:N}";
            await identity.Database.ExecuteSqlInterpolatedAsync($"select pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);
            User? user = await identity.Users.SingleOrDefaultAsync(candidate => candidate.Id == requestedUserId && candidate.OrganizationId == device.OrganizationId, ct);
            PinCredential? credential = user is null ? null : await identity.PinCredentials.FromSqlInterpolated($"select * from identity.pin_credentials where user_id = {requestedUserId} for update").SingleOrDefaultAsync(ct);
            bool activeRole = user is not null && await (
                from assignment in identity.UserRoleAssignments
                join role in identity.Roles on new { assignment.RoleId, assignment.OrganizationId } equals new { RoleId = role.Id, role.OrganizationId }
                where assignment.UserId == user.Id && assignment.RevokedAtUtc == null && role.Status == RoleStatus.Active
                select assignment.Id).AnyAsync(ct);
            bool branchAccess = user is not null && await HasBranchAccessAsync(organization, device, user.Id, ct);
            bool eligible = user?.Status == UserStatus.Active && credential is not null && activeRole && branchAccess;
            DateTimeOffset windowStart = now.AddMinutes(-_options.FailureWindowMinutes);
            DateTimeOffset? lastSuccess = await identity.PinLoginAttempts.Where(attempt => attempt.DeviceId == device.DeviceId && attempt.UserId == requestedUserId && attempt.Outcome == LoginAttemptOutcome.Succeeded).MaxAsync(attempt => (DateTimeOffset?)attempt.OccurredAtUtc, ct);
            DateTimeOffset effectiveStart = lastSuccess is not null && lastSuccess > windowStart ? lastSuccess.Value : windowStart;
            DateTimeOffset[] failures = await identity.PinLoginAttempts.Where(attempt => attempt.DeviceId == device.DeviceId && attempt.UserId == requestedUserId && attempt.OccurredAtUtc >= effectiveStart && attempt.Outcome != LoginAttemptOutcome.Succeeded).OrderBy(attempt => attempt.OccurredAtUtc).Select(attempt => attempt.OccurredAtUtc).ToArrayAsync(ct);
            bool locked = failures.Length >= _options.FailureThreshold && now < failures[^1].AddMinutes(_options.LockoutMinutes);
            bool verified = eligible && !locked ? _pinHasher.Verify(pin, credential!.EncodedHash) : _pinHasher.Verify(pin, _dummy.EncodedHash);
            LoginAttemptOutcome outcome = !eligible ? LoginAttemptOutcome.Ineligible : locked ? LoginAttemptOutcome.Locked : verified ? LoginAttemptOutcome.Succeeded : failures.Length + 1 >= _options.FailureThreshold ? LoginAttemptOutcome.Locked : LoginAttemptOutcome.InvalidCredentials;
            identity.PinLoginAttempts.Add(PinLoginAttempt.Create(Guid.NewGuid(), device.DeviceId, user?.Id, outcome, now, correlationId));
            if (outcome != LoginAttemptOutcome.Succeeded)
            {
                await AppendAuditAsync(audit, now, device, null, user?.Id, AuditActorType.Anonymous, AuditAction.PinLoginFailed, AuditResult.Denied, outcome.ToString(), correlationId, ct);
                return DevicePinLoginResult.Failed;
            }
            UserSession session = UserSession.CreateDeviceBound(Guid.NewGuid(), user!.Id, device.DeviceId, device.BranchId, device.SecurityVersion, credential!.Version, user.AuthorizationVersion, now, TimeSpan.FromMinutes(_options.InactivityMinutes), TimeSpan.FromHours(_options.AbsoluteLifetimeHours));
            identity.UserSessions.Add(session);
            await AppendAuditAsync(audit, now, device, user.Id, user.Id, AuditActorType.User, AuditAction.PinLoginSucceeded, AuditResult.Succeeded, null, correlationId, ct);
            return new DevicePinLoginResult(true, session.Id, new ManagementSessionSnapshot(session.Id, user.Id, user.OrganizationId, user.Username, user.DisplayName, user.PreferredLanguage, session.InactivityExpiresAtUtc, session.AbsoluteExpiresAtUtc, session.LastReauthenticatedAtUtc, EffectiveAuthorizationSnapshot.Empty(user.Id, user.OrganizationId)));
        }, token);

    public Task<bool> LockAsync(Guid sessionId, string correlationId, CancellationToken token)
        => ExecuteAsync(async (organization, identity, audit, now, ct) =>
        {
            UserSession? session = await identity.UserSessions.FromSqlInterpolated($"select * from identity.user_sessions where id = {sessionId} for update").SingleOrDefaultAsync(ct);
            if (session is null || session.RevokedAtUtc is not null || session.DeviceId is null || session.BranchId is null) return false;
            User user = await identity.Users.SingleAsync(candidate => candidate.Id == session.UserId, ct);
            DeviceRequestContext device = new(session.DeviceId.Value, user.OrganizationId, session.BranchId.Value, session.DeviceSecurityVersion ?? 0, string.Empty, DeviceType.PosTerminal);
            session.Revoke(SessionRevocationReason.WorkstationLocked, now);
            await AppendAuditAsync(audit, now, device, user.Id, user.Id, AuditActorType.User, AuditAction.WorkstationLocked, AuditResult.Succeeded, null, correlationId, ct);
            return true;
        }, token);

    private async Task<T> ExecuteAsync<T>(Func<OrganizationDbContext, IdentityDbContext, IAuditWriter, DateTimeOffset, CancellationToken, Task<T>> operation, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(_connectionString); await connection.OpenAsync(token); await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        await using var organization = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(connection).Options); await using var identity = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options); await using var auditContext = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options);
        await organization.Database.UseTransactionAsync(transaction, token); await identity.Database.UseTransactionAsync(transaction, token); await auditContext.Database.UseTransactionAsync(transaction, token);
        try { T result = await operation(organization, identity, new AuditWriter(auditContext), _clock.UtcNow, token); await organization.SaveChangesAsync(token); await identity.SaveChangesAsync(token); await auditContext.SaveChangesAsync(token); await transaction.CommitAsync(token); return result; }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
    }

    private static async Task<bool> HasBranchAccessAsync(OrganizationDbContext organization, DeviceRequestContext device, Guid userId, CancellationToken token)
    {
        UserBranchAccess? access = await organization.UserBranchAccesses.SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.OrganizationId == device.OrganizationId, token);
        if (access is null) return false; if (access.Scope == BranchAccessScope.AllOrganizationBranches) return true;
        return await organization.UserBranchAssignments.AnyAsync(assignment => assignment.AccessId == access.Id && assignment.BranchId == device.BranchId && assignment.RevokedAtUtc == null, token);
    }
    private static Task AppendAuditAsync(IAuditWriter audit, DateTimeOffset now, DeviceRequestContext device, Guid? actorId, Guid? userId, AuditActorType actorType, AuditAction action, AuditResult result, string? reason, string correlationId, CancellationToken token)
        => audit.AppendAsync(new AuditWriteRequest(Guid.NewGuid(), now, device.OrganizationId, device.BranchId, null, actorId, actorType, device.DeviceId, action, "User", userId, result, reason, correlationId, null, null, null, null), token);
}
public sealed record DevicePinLoginResult(bool Succeeded, Guid SessionId, ManagementSessionSnapshot? Session)
{
    public static DevicePinLoginResult Failed { get; } = new(false, Guid.Empty, null);
}
