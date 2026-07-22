using System.Text.Json;
using Kalm.Api.Configuration;
using Kalm.Api.Features.DeviceAdministration;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.Transactions;

public sealed class DeviceAdministrationAuditTransactionCoordinator
{
    private readonly string _connectionString;
    private readonly IClock _clock;
    public DeviceAdministrationAuditTransactionCoordinator(IOptions<DatabaseOptions> database, IClock clock) { _connectionString = database.Value.ConnectionString; _clock = clock; }

    public Task<DeviceOperationResult> CreateAsync(Guid organizationId, Guid actorId, DeviceCreateRequest request, string correlationId, CancellationToken token)
        => ExecuteAsync(organizationId, actorId, correlationId, async (organization, identity, audit, now, ct) =>
        {
            if (!DeviceAdministrationQueries.TryType(request.Type, out DeviceType type)) return DeviceOperationResult.Failure("device.validation_failed");
            bool branchValid = await organization.Branches.AnyAsync(branch => branch.Id == request.BranchId && branch.OrganizationId == organizationId && branch.Status == BranchStatus.Active, ct);
            if (!branchValid) return DeviceOperationResult.Failure("device.branch_invalid");
            Device device;
            try { device = Device.Register(Guid.NewGuid(), organizationId, request.BranchId, request.Name, type, request.Platform, now); }
            catch (ArgumentException) { return DeviceOperationResult.Failure("device.validation_failed"); }
            organization.Devices.Add(device);
            await AppendAuditAsync(audit, now, organizationId, request.BranchId, actorId, AuditActorType.User, device.Id, AuditAction.DeviceRegistered, null,
                JsonSerializer.Serialize(new { device.Name, type = DeviceAdministrationQueries.Type(device.Type), status = "pendingPairing" }), correlationId, ct);
            return DeviceOperationResult.Success(device.Id, device.Version);
        }, token);

    public Task<DeviceOperationResult> UpdateAsync(Guid organizationId, Guid actorId, Guid deviceId, long expectedVersion, DeviceUpdateRequest request, string correlationId, CancellationToken token)
        => ExecuteAsync(organizationId, actorId, correlationId, async (organization, identity, audit, now, ct) =>
        {
            Device? device = await LockDeviceAsync(organization, organizationId, deviceId, ct);
            if (device is null) return DeviceOperationResult.Failure("device.not_found");
            if (device.Version != expectedVersion) return DeviceOperationResult.Failure("device.concurrency_conflict", device.Version);
            if (!DeviceAdministrationQueries.TryType(request.Type, out DeviceType type)) return DeviceOperationResult.Failure("device.validation_failed");
            bool branchValid = await organization.Branches.AnyAsync(branch => branch.Id == request.BranchId && branch.OrganizationId == organizationId && branch.Status == BranchStatus.Active, ct);
            if (!branchValid) return DeviceOperationResult.Failure("device.branch_invalid");
            long beforeVersion = device.Version;
            bool securityChanged;
            try { securityChanged = device.Update(request.BranchId, request.Name, type, request.Platform, now); }
            catch (InvalidOperationException) { return DeviceOperationResult.Failure("device.revoked"); }
            catch (ArgumentException) { return DeviceOperationResult.Failure("device.validation_failed"); }
            if (device.Version == beforeVersion) return DeviceOperationResult.Success(device.Id, device.Version);
            if (securityChanged) await InvalidateDeviceSecurityAsync(organization, identity, device.Id, now, SessionRevocationReason.DeviceRepaired, ct);
            await AppendAuditAsync(audit, now, organizationId, device.BranchId, actorId, AuditActorType.User, device.Id, AuditAction.DeviceUpdated, null,
                JsonSerializer.Serialize(new { device.Name, type = DeviceAdministrationQueries.Type(device.Type), securityChanged }), correlationId, ct);
            return DeviceOperationResult.Success(device.Id, device.Version);
        }, token);

    public Task<DeviceChallengeResult> CreateChallengeAsync(Guid organizationId, Guid actorId, Guid deviceId, string correlationId, CancellationToken token)
    {
        string plaintext = DeviceSecurity.GenerateChallenge();
        string hash = DeviceSecurity.Hash(plaintext);
        return ExecuteAsync(organizationId, actorId, correlationId, async (organization, identity, audit, now, ct) =>
        {
            Device? device = await LockDeviceAsync(organization, organizationId, deviceId, ct);
            if (device is null) return DeviceChallengeResult.Failure("device.not_found");
            if (device.Status == DeviceStatus.Revoked) return DeviceChallengeResult.Failure("device.revoked");
            DevicePairingChallenge[] previous = await organization.DevicePairingChallenges.Where(challenge => challenge.DeviceId == deviceId && challenge.ConsumedAtUtc == null && challenge.InvalidatedAtUtc == null).ToArrayAsync(ct);
            foreach (DevicePairingChallenge challenge in previous) challenge.Invalidate(now);
            DateTimeOffset expires = now.AddMinutes(10);
            organization.DevicePairingChallenges.Add(DevicePairingChallenge.Create(Guid.NewGuid(), deviceId, hash, now, expires));
            await AppendAuditAsync(audit, now, organizationId, device.BranchId, actorId, AuditActorType.User, device.Id, AuditAction.DevicePairingChallengeCreated, null,
                JsonSerializer.Serialize(new { expiresAtUtc = expires }), correlationId, ct);
            return DeviceChallengeResult.Success(device.Id, plaintext, expires);
        }, token);
    }

    public Task<DeviceOperationResult> RevokeAsync(Guid organizationId, Guid actorId, Guid deviceId, long expectedVersion, string correlationId, CancellationToken token)
        => ExecuteAsync(organizationId, actorId, correlationId, async (organization, identity, audit, now, ct) =>
        {
            Device? device = await LockDeviceAsync(organization, organizationId, deviceId, ct);
            if (device is null) return DeviceOperationResult.Failure("device.not_found");
            if (device.Version != expectedVersion) return DeviceOperationResult.Failure("device.concurrency_conflict", device.Version);
            if (device.Revoke(now))
            {
                await InvalidateDeviceSecurityAsync(organization, identity, device.Id, now, SessionRevocationReason.DeviceRevoked, ct);
                await AppendAuditAsync(audit, now, organizationId, device.BranchId, actorId, AuditActorType.User, device.Id, AuditAction.DeviceRevoked, null,
                    JsonSerializer.Serialize(new { status = "revoked" }), correlationId, ct);
            }
            return DeviceOperationResult.Success(device.Id, device.Version);
        }, token);

    public Task<DevicePairResult> PairAsync(Guid deviceId, string challengeValue, string correlationId, CancellationToken token)
    {
        string hash = DeviceSecurity.Hash(challengeValue ?? string.Empty);
        string credentialValue = DeviceSecurity.GenerateCredential();
        string credentialHash = DeviceSecurity.Hash(credentialValue);
        return ExecuteAsync(null, null, correlationId, async (organization, identity, audit, now, ct) =>
        {
            DevicePairingChallenge? challenge = await organization.DevicePairingChallenges
                .FromSqlInterpolated($"select * from organization.device_pairing_challenges where challenge_hash = {hash} for update")
                .SingleOrDefaultAsync(ct);
            Device? device = await organization.Devices.FromSqlInterpolated($"select * from organization.devices where id = {deviceId} for update").SingleOrDefaultAsync(ct);
            if (challenge is null || device is null || challenge.DeviceId != device.Id || !challenge.IsUsable(now) || device.Status == DeviceStatus.Revoked)
                return DevicePairResult.Failure;
            bool branchValid = await organization.Branches.AnyAsync(branch => branch.Id == device.BranchId && branch.OrganizationId == device.OrganizationId && branch.Status == BranchStatus.Active, ct);
            if (!branchValid) return DevicePairResult.Failure;
            challenge.Consume(now);
            bool rotation = await organization.DeviceCredentials.AnyAsync(candidate => candidate.DeviceId == device.Id, ct);
            await InvalidateDeviceSecurityAsync(organization, identity, device.Id, now, SessionRevocationReason.DeviceRepaired, ct);
            device.Pair(now);
            organization.DeviceCredentials.Add(DeviceCredential.Issue(Guid.NewGuid(), device.Id, credentialHash, device.SecurityVersion, now));
            await AppendAuditAsync(audit, now, device.OrganizationId, device.BranchId, null, AuditActorType.Anonymous, device.Id,
                rotation ? AuditAction.DeviceCredentialRotated : AuditAction.DevicePaired, null,
                JsonSerializer.Serialize(new { deviceId = device.Id, branchId = device.BranchId }), correlationId, ct);
            return DevicePairResult.Success(credentialValue);
        }, token);
    }

    private async Task<T> ExecuteAsync<T>(Guid? organizationId, Guid? actorId, string correlationId, Func<OrganizationDbContext, IdentityDbContext, IAuditWriter, DateTimeOffset, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString); await connection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var organization = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(connection).Options);
        await using var identity = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options);
        await using var auditContext = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options);
        await organization.Database.UseTransactionAsync(transaction, cancellationToken); await identity.Database.UseTransactionAsync(transaction, cancellationToken); await auditContext.Database.UseTransactionAsync(transaction, cancellationToken);
        try
        {
            T result = await operation(organization, identity, new AuditWriter(auditContext), _clock.UtcNow, cancellationToken);
            await organization.SaveChangesAsync(cancellationToken); await identity.SaveChangesAsync(cancellationToken); await auditContext.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
    }

    private static Task<Device?> LockDeviceAsync(OrganizationDbContext context, Guid organizationId, Guid deviceId, CancellationToken token)
        => context.Devices.FromSqlInterpolated($"select * from organization.devices where id = {deviceId} and organization_id = {organizationId} for update").SingleOrDefaultAsync(token);

    private static async Task InvalidateDeviceSecurityAsync(OrganizationDbContext organization, IdentityDbContext identity, Guid deviceId, DateTimeOffset now, SessionRevocationReason reason, CancellationToken token)
    {
        DeviceCredential[] credentials = await organization.DeviceCredentials.Where(credential => credential.DeviceId == deviceId && credential.RevokedAtUtc == null).ToArrayAsync(token);
        foreach (DeviceCredential credential in credentials) credential.Revoke(now);
        DevicePairingChallenge[] challenges = await organization.DevicePairingChallenges.Where(challenge => challenge.DeviceId == deviceId && challenge.ConsumedAtUtc == null && challenge.InvalidatedAtUtc == null).ToArrayAsync(token);
        foreach (DevicePairingChallenge challenge in challenges) challenge.Invalidate(now);
        UserSession[] sessions = await identity.UserSessions.Where(session => session.DeviceId == deviceId && session.RevokedAtUtc == null).ToArrayAsync(token);
        foreach (UserSession session in sessions) session.Revoke(reason, now);
    }

    private static Task AppendAuditAsync(IAuditWriter audit, DateTimeOffset now, Guid? organizationId, Guid? branchId, Guid? actorId, AuditActorType actorType, Guid deviceId, AuditAction action, string? before, string? after, string correlationId, CancellationToken token)
        => audit.AppendAsync(new AuditWriteRequest(Guid.NewGuid(), now, organizationId, branchId, null, actorId, actorType, deviceId, action, "Device", deviceId, AuditResult.Succeeded, null, correlationId, before, after, null, null), token);
}

public sealed record DeviceOperationResult(bool Succeeded, Guid DeviceId, long Version, string? ErrorCode, long? CurrentVersion)
{
    public static DeviceOperationResult Success(Guid id, long version) => new(true, id, version, null, null);
    public static DeviceOperationResult Failure(string code, long? current = null) => new(false, Guid.Empty, 0, code, current);
}
public sealed record DeviceChallengeResult(bool Succeeded, Guid DeviceId, string? Challenge, DateTimeOffset? ExpiresAtUtc, string? ErrorCode)
{
    public static DeviceChallengeResult Success(Guid id, string challenge, DateTimeOffset expires) => new(true, id, challenge, expires, null);
    public static DeviceChallengeResult Failure(string code) => new(false, Guid.Empty, null, null, code);
}
public sealed record DevicePairResult(bool Succeeded, string? Credential)
{
    public static DevicePairResult Success(string value) => new(true, value);
    public static DevicePairResult Failure { get; } = new(false, null);
}
