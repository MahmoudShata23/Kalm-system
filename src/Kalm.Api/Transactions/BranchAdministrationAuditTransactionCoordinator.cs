using System.Text.Json;
using Kalm.Api.Configuration;
using Kalm.Api.Features.BranchAdministration;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization;
using Kalm.Organization.Domain;
using Kalm.Organization.Domain.ValueObjects;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.Transactions;

public sealed class BranchAdministrationAuditTransactionCoordinator
{
    private readonly string _connectionString;
    private readonly IClock _clock;

    public BranchAdministrationAuditTransactionCoordinator(IOptions<DatabaseOptions> database, IClock clock)
    {
        _connectionString = database.Value.ConnectionString;
        _clock = clock;
    }

    public async Task<BranchOperationResult> CreateAsync(
        Guid organizationId,
        Guid actorId,
        BranchWriteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        BranchInput? input = Parse(request);
        if (input is null)
        {
            return BranchOperationResult.Failure("branch.validation_failed");
        }

        try
        {
            return await ExecuteAsync(async (organization, _, audit, now, token) =>
            {
                await AcquireCodeLockAsync(organization, organizationId, token);
                if (await organization.Branches.AnyAsync(
                    branch => branch.OrganizationId == organizationId && branch.Code == input.Code.Value,
                    token))
                {
                    return BranchOperationResult.Failure("branch.code_reserved");
                }

                Branch branch = Branch.Create(
                    Guid.NewGuid(),
                    organizationId,
                    input.Name,
                    input.Code,
                    input.Locale,
                    input.TimeZone,
                    input.Rollover,
                    now);
                organization.Branches.Add(branch);
                await AppendAuditAsync(
                    audit,
                    now,
                    organizationId,
                    branch.Id,
                    actorId,
                    AuditAction.BranchCreated,
                    null,
                    SafeState(branch),
                    null,
                    correlationId,
                    token);
                return BranchOperationResult.Success(branch.Id, branch.Version);
            }, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return BranchOperationResult.Failure("branch.code_reserved");
        }
    }

    public async Task<BranchOperationResult> UpdateAsync(
        Guid organizationId,
        Guid actorId,
        Guid branchId,
        long expectedVersion,
        BranchWriteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        BranchInput? input = Parse(request);
        if (input is null)
        {
            return BranchOperationResult.Failure("branch.validation_failed");
        }

        try
        {
            return await ExecuteAsync(async (organization, _, audit, now, token) =>
            {
                await BranchMutationLock.AcquireAsync(organization, organizationId, [branchId], token);
                Branch? branch = await LockBranchAsync(organization, organizationId, branchId, token);
                if (branch is null)
                {
                    return BranchOperationResult.Failure("branch.not_found");
                }

                if (branch.Version != expectedVersion)
                {
                    return BranchOperationResult.Failure("branch.concurrency_conflict", branch.Version);
                }

                if (branch.Status == BranchStatus.Archived)
                {
                    return BranchOperationResult.Failure("branch.archived");
                }

                await AcquireCodeLockAsync(organization, organizationId, token);
                if (await organization.Branches.AnyAsync(
                    candidate => candidate.OrganizationId == organizationId
                        && candidate.Id != branchId
                        && candidate.Code == input.Code.Value,
                    token))
                {
                    return BranchOperationResult.Failure("branch.code_reserved");
                }

                string before = SafeState(branch);
                List<string> changedFields = ChangedFields(branch, input);
                if (!branch.Update(input.Name, input.Code, input.Locale, input.TimeZone, input.Rollover, now))
                {
                    return BranchOperationResult.Success(branch.Id, branch.Version);
                }

                await AppendAuditAsync(
                    audit,
                    now,
                    organizationId,
                    branch.Id,
                    actorId,
                    AuditAction.BranchUpdated,
                    before,
                    JsonSerializer.Serialize(new { branch = SafeStateObject(branch), changedFields }),
                    null,
                    correlationId,
                    token);
                return BranchOperationResult.Success(branch.Id, branch.Version);
            }, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return BranchOperationResult.Failure("branch.code_reserved");
        }
    }

    public Task<BranchOperationResult> ActivateAsync(
        Guid organizationId,
        Guid actorId,
        Guid branchId,
        long expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
        => ChangeStatusAsync(organizationId, actorId, branchId, expectedVersion, true, correlationId, cancellationToken);

    public Task<BranchOperationResult> DeactivateAsync(
        Guid organizationId,
        Guid actorId,
        Guid branchId,
        long expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
        => ChangeStatusAsync(organizationId, actorId, branchId, expectedVersion, false, correlationId, cancellationToken);

    private Task<BranchOperationResult> ChangeStatusAsync(
        Guid organizationId,
        Guid actorId,
        Guid branchId,
        long expectedVersion,
        bool activate,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(async (organization, identity, audit, now, token) =>
        {
            await BranchMutationLock.AcquireAsync(organization, organizationId, [branchId], token);
            Branch? branch = await LockBranchAsync(organization, organizationId, branchId, token);
            if (branch is null)
            {
                return BranchOperationResult.Failure("branch.not_found");
            }

            if (branch.Version != expectedVersion)
            {
                return BranchOperationResult.Failure("branch.concurrency_conflict", branch.Version);
            }

            if (branch.Status == BranchStatus.Archived)
            {
                return BranchOperationResult.Failure("branch.archived");
            }

            if (!activate)
            {
                if (branch.Status == BranchStatus.Suspended)
                {
                    return BranchOperationResult.Success(branch.Id, branch.Version);
                }

                BranchDependencyCountsResponse dependencies = await CountDependenciesAsync(
                    organization,
                    identity,
                    organizationId,
                    branchId,
                    token);
                if (HasDependencies(dependencies))
                {
                    await AppendAuditAsync(
                        audit,
                        now,
                        organizationId,
                        branch.Id,
                        actorId,
                        AuditAction.BranchAdministrationRejected,
                        SafeState(branch),
                        JsonSerializer.Serialize(new { dependencyCounts = dependencies }),
                        "branch.dependencies_active",
                        correlationId,
                        token);
                    return BranchOperationResult.Failure("branch.dependencies_active", dependencies: dependencies);
                }
            }

            string previousStatus = BranchAdministrationQueries.Status(branch.Status);
            bool changed = activate ? branch.Activate(now) : branch.Deactivate(now);
            if (!changed)
            {
                return BranchOperationResult.Success(branch.Id, branch.Version);
            }

            await AppendAuditAsync(
                audit,
                now,
                organizationId,
                branch.Id,
                actorId,
                activate ? AuditAction.BranchActivated : AuditAction.BranchDeactivated,
                JsonSerializer.Serialize(new { status = previousStatus }),
                JsonSerializer.Serialize(new { status = BranchAdministrationQueries.Status(branch.Status) }),
                null,
                correlationId,
                token);
            return BranchOperationResult.Success(branch.Id, branch.Version);
        }, cancellationToken);

    private async Task<T> ExecuteAsync<T>(
        Func<OrganizationDbContext, IdentityDbContext, IAuditWriter, DateTimeOffset, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var organization = new OrganizationDbContext(
            new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(connection).Options);
        await using var identity = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options);
        await using var auditContext = new AuditDbContext(
            new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options);
        await organization.Database.UseTransactionAsync(transaction, cancellationToken);
        await identity.Database.UseTransactionAsync(transaction, cancellationToken);
        await auditContext.Database.UseTransactionAsync(transaction, cancellationToken);
        try
        {
            T result = await operation(organization, identity, new AuditWriter(auditContext), _clock.UtcNow, cancellationToken);
            await organization.SaveChangesAsync(cancellationToken);
            await identity.SaveChangesAsync(cancellationToken);
            await auditContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static BranchInput? Parse(BranchWriteRequest request)
    {
        try
        {
            return new BranchInput(
                new OrganizationName(request.Name, 120),
                new BranchCode(request.Code),
                new LocaleCode(request.LocaleCode),
                new TimeZoneId(request.TimeZoneId),
                BusinessDayRollover.Parse(request.BusinessDayRollover));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Task<Branch?> LockBranchAsync(
        OrganizationDbContext organization,
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
        => organization.Branches
            .FromSqlInterpolated($"select * from organization.branches where id = {branchId} and organization_id = {organizationId} for update")
            .SingleOrDefaultAsync(cancellationToken);

    private static async Task AcquireCodeLockAsync(
        OrganizationDbContext organization,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        string lockKey = $"branch-code:{organizationId:D}";
        await organization.Database.ExecuteSqlInterpolatedAsync(
            $"select pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }

    private static async Task<BranchDependencyCountsResponse> CountDependenciesAsync(
        OrganizationDbContext organization,
        IdentityDbContext identity,
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        int registeredDevices = await organization.Devices.CountAsync(
            device => device.OrganizationId == organizationId
                && device.BranchId == branchId
                && device.Status == DeviceStatus.PendingPairing,
            cancellationToken);
        int activeDevices = await organization.Devices.CountAsync(
            device => device.OrganizationId == organizationId
                && device.BranchId == branchId
                && device.Status == DeviceStatus.Active,
            cancellationToken);
        int activeCredentials = await (
            from credential in organization.DeviceCredentials
            join device in organization.Devices on credential.DeviceId equals device.Id
            where device.OrganizationId == organizationId
                && device.BranchId == branchId
                && credential.RevokedAtUtc == null
            select credential.Id).CountAsync(cancellationToken);
        int activeSessions = await (
            from session in identity.UserSessions
            join user in identity.Users on session.UserId equals user.Id
            where user.OrganizationId == organizationId
                && session.BranchId == branchId
                && session.RevokedAtUtc == null
            select session.Id).CountAsync(cancellationToken);
        int activeAssignments = await organization.UserBranchAssignments.CountAsync(
            assignment => assignment.OrganizationId == organizationId
                && assignment.BranchId == branchId
                && assignment.RevokedAtUtc == null,
            cancellationToken);
        return new BranchDependencyCountsResponse(
            registeredDevices,
            activeDevices,
            activeCredentials,
            activeSessions,
            activeAssignments);
    }

    private static bool HasDependencies(BranchDependencyCountsResponse counts)
        => counts.RegisteredDeviceCount > 0
            || counts.ActiveDeviceCount > 0
            || counts.ActiveCredentialCount > 0
            || counts.ActiveSessionCount > 0
            || counts.ActiveUserAssignmentCount > 0;

    private static List<string> ChangedFields(Branch branch, BranchInput input)
    {
        var fields = new List<string>(5);
        if (branch.Name != input.Name.Value) fields.Add("name");
        if (branch.Code != input.Code.Value) fields.Add("code");
        if (branch.LocaleCode != input.Locale.Value) fields.Add("localeCode");
        if (branch.TimeZoneId != input.TimeZone.Value) fields.Add("timeZoneId");
        if (branch.BusinessDayRollover != input.Rollover.Value) fields.Add("businessDayRollover");
        return fields;
    }

    private static string SafeState(Branch branch) => JsonSerializer.Serialize(SafeStateObject(branch));

    private static object SafeStateObject(Branch branch) => new
    {
        branchId = branch.Id,
        branch.Name,
        branch.Code,
        branch.LocaleCode,
        branch.TimeZoneId,
        businessDayRollover = branch.BusinessDayRollover.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
        status = BranchAdministrationQueries.Status(branch.Status)
    };

    private static Task AppendAuditAsync(
        IAuditWriter audit,
        DateTimeOffset now,
        Guid organizationId,
        Guid branchId,
        Guid actorId,
        AuditAction action,
        string? before,
        string? after,
        string? reasonCode,
        string correlationId,
        CancellationToken cancellationToken)
        => audit.AppendAsync(
            new AuditWriteRequest(
                Guid.NewGuid(),
                now,
                organizationId,
                branchId,
                null,
                actorId,
                AuditActorType.User,
                null,
                action,
                "Branch",
                branchId,
                reasonCode is null ? AuditResult.Succeeded : AuditResult.Denied,
                reasonCode,
                correlationId,
                before,
                after,
                null,
                null),
            cancellationToken);

    private sealed record BranchInput(
        OrganizationName Name,
        BranchCode Code,
        LocaleCode Locale,
        TimeZoneId TimeZone,
        BusinessDayRollover Rollover);
}

public sealed record BranchOperationResult(
    bool Succeeded,
    Guid BranchId,
    long Version,
    string? ErrorCode,
    long? CurrentVersion,
    BranchDependencyCountsResponse? Dependencies)
{
    public static BranchOperationResult Success(Guid branchId, long version)
        => new(true, branchId, version, null, null, null);

    public static BranchOperationResult Failure(
        string errorCode,
        long? currentVersion = null,
        BranchDependencyCountsResponse? dependencies = null)
        => new(false, Guid.Empty, 0, errorCode, currentVersion, dependencies);
}
