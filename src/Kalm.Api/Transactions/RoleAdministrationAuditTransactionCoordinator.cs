using System.Data;
using System.Text.Json;
using Kalm.Api.Configuration;
using Kalm.Api.Features.RoleAdministration;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity;
using Kalm.Identity.Authorization;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.Transactions;

public sealed class RoleAdministrationAuditTransactionCoordinator
{
    private const string LastManagementAccessConstraint = "ck_identity_last_management_access";
    private readonly string _connectionString;
    private readonly IClock _clock;

    public RoleAdministrationAuditTransactionCoordinator(IOptions<DatabaseOptions> database, IClock clock)
    {
        _connectionString = database.Value.ConnectionString;
        _clock = clock;
    }

    public Task<RoleOperationResult> CreateAsync(
        Guid organizationId,
        Guid actorId,
        RoleWriteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            organizationId,
            actorId,
            correlationId,
            async (identity, audit, now, token) =>
            {
                RoleInputValidation validation = ValidateInput(request);
                if (!validation.Succeeded)
                {
                    return RoleOperationResult.Failure(validation.ErrorCode!);
                }

                Permission[]? permissions = await ResolvePermissionsAsync(identity, validation.PermissionCodes, token);
                if (permissions is null)
                {
                    return RoleOperationResult.Failure("authorization.permission_catalogue_unavailable");
                }

                if (await identity.Roles.AnyAsync(
                    role => role.OrganizationId == organizationId && role.NormalizedName == validation.Name!.NormalizedValue,
                    token))
                {
                    return RoleOperationResult.Failure("role.name_conflict");
                }

                var role = Role.Create(Guid.NewGuid(), organizationId, validation.Name!, null, now);
                identity.Roles.Add(role);
                foreach (Permission permission in permissions)
                {
                    identity.RolePermissions.Add(RolePermission.Grant(Guid.NewGuid(), role.Id, permission.Id, now));
                }

                await AppendAuditAsync(
                    audit,
                    now,
                    organizationId,
                    actorId,
                    AuditAction.RoleCreated,
                    role.Id,
                    null,
                    SafeJson(new { name = role.Name, permissionCodes = validation.PermissionCodes }),
                    null,
                    correlationId,
                    token);

                return RoleOperationResult.Success(ToDetail(role, validation.PermissionCodes, 0));
            },
            cancellationToken);

    public Task<RoleOperationResult> UpdateAsync(
        Guid organizationId,
        Guid actorId,
        Guid roleId,
        long expectedVersion,
        RoleWriteRequest request,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            organizationId,
            actorId,
            correlationId,
            async (identity, audit, now, token) =>
            {
                RoleInputValidation validation = ValidateInput(request);
                if (!validation.Succeeded)
                {
                    return RoleOperationResult.Failure(validation.ErrorCode!);
                }

                await AcquireManagementAccessLockAsync(identity, organizationId, token);
                Role? role = await identity.Roles
                    .FromSqlInterpolated($"select * from identity.roles where id = {roleId} and organization_id = {organizationId} for update")
                    .SingleOrDefaultAsync(token);
                if (role is null)
                {
                    return RoleOperationResult.Failure("role.not_found");
                }

                if (role.Version != expectedVersion)
                {
                    return RoleOperationResult.Failure("role.concurrency_conflict", role.Version);
                }

                if (role.IsProtectedSystemRole)
                {
                    return RoleOperationResult.AuditedFailure("role.system_role_protected", role.Id, AuditAction.RoleAdministrationRejected);
                }

                if (role.Status == RoleStatus.Archived)
                {
                    return RoleOperationResult.Failure("role.archived");
                }

                Permission[]? permissions = await ResolvePermissionsAsync(identity, validation.PermissionCodes, token);
                if (permissions is null)
                {
                    return RoleOperationResult.Failure("authorization.permission_catalogue_unavailable");
                }

                if (await identity.Roles.AnyAsync(
                    candidate => candidate.OrganizationId == organizationId
                        && candidate.Id != roleId
                        && candidate.NormalizedName == validation.Name!.NormalizedValue,
                    token))
                {
                    return RoleOperationResult.Failure("role.name_conflict");
                }

                RolePermission[] activeGrants = await identity.RolePermissions
                    .Where(grant => grant.RoleId == roleId && grant.RevokedAtUtc == null)
                    .ToArrayAsync(token);
                Dictionary<Guid, Permission> permissionsById = (await identity.Permissions
                    .Where(permission => activeGrants.Select(grant => grant.PermissionId).Contains(permission.Id))
                    .ToArrayAsync(token))
                    .ToDictionary(permission => permission.Id);
                string[] currentCodes = activeGrants
                    .Select(grant => permissionsById[grant.PermissionId].Code)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray();
                string[] addedCodes = validation.PermissionCodes.Except(currentCodes, StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray();
                string[] removedCodes = currentCodes.Except(validation.PermissionCodes, StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray();
                bool permissionSetChanged = addedCodes.Length > 0 || removedCodes.Length > 0;
                bool nameChanged = !string.Equals(role.Name, validation.Name!.Value, StringComparison.Ordinal);
                if (!nameChanged && !permissionSetChanged)
                {
                    int unchangedAssignmentCount = await ActiveAssignmentCountAsync(identity, roleId, token);
                    return RoleOperationResult.Success(ToDetail(role, currentCodes, unchangedAssignmentCount));
                }

                if (permissionSetChanged)
                {
                    Guid[] affectedUserIds = await identity.UserRoleAssignments
                        .Where(assignment => assignment.RoleId == roleId && assignment.RevokedAtUtc == null)
                        .Select(assignment => assignment.UserId)
                        .Distinct()
                        .OrderBy(id => id)
                        .ToArrayAsync(token);
                    if (affectedUserIds.Length > 0)
                    {
                        User[] affectedUsers = await identity.Users
                            .FromSqlRaw(
                                "select * from identity.users where id = any ({0}) order by id for update",
                                affectedUserIds)
                            .ToArrayAsync(token);
                        foreach (User user in affectedUsers)
                        {
                            user.AdvanceAuthorizationVersion(now);
                        }
                    }

                    HashSet<string> removed = removedCodes.ToHashSet(StringComparer.Ordinal);
                    foreach (RolePermission grant in activeGrants.Where(grant => removed.Contains(permissionsById[grant.PermissionId].Code)))
                    {
                        grant.Revoke(now);
                    }

                    Dictionary<string, Permission> requestedByCode = permissions.ToDictionary(permission => permission.Code, StringComparer.Ordinal);
                    foreach (string code in addedCodes)
                    {
                        identity.RolePermissions.Add(RolePermission.Grant(Guid.NewGuid(), roleId, requestedByCode[code].Id, now));
                    }
                }

                string oldName = role.Name;
                role.UpdateDefinition(validation.Name!, permissionSetChanged, now);
                if (nameChanged)
                {
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.RoleRenamed, role.Id,
                        SafeJson(new { name = oldName }), SafeJson(new { name = role.Name }), null, correlationId, token);
                }

                if (permissionSetChanged)
                {
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.RolePermissionSetChanged, role.Id,
                        SafeJson(new { permissionCodes = currentCodes }),
                        SafeJson(new { permissionCodes = validation.PermissionCodes, addedPermissionCodes = addedCodes, removedPermissionCodes = removedCodes }),
                        null, correlationId, token);
                }

                int assignmentCount = await ActiveAssignmentCountAsync(identity, roleId, token);
                return RoleOperationResult.Success(ToDetail(role, validation.PermissionCodes, assignmentCount));
            },
            cancellationToken);

    public Task<RoleOperationResult> ArchiveAsync(
        Guid organizationId,
        Guid actorId,
        Guid roleId,
        long expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            organizationId,
            actorId,
            correlationId,
            async (identity, audit, now, token) =>
            {
                await AcquireManagementAccessLockAsync(identity, organizationId, token);
                Role? role = await identity.Roles
                    .FromSqlInterpolated($"select * from identity.roles where id = {roleId} and organization_id = {organizationId} for update")
                    .SingleOrDefaultAsync(token);
                if (role is null)
                {
                    return RoleOperationResult.Failure("role.not_found");
                }

                if (role.Version != expectedVersion)
                {
                    return RoleOperationResult.Failure("role.concurrency_conflict", role.Version);
                }

                if (role.IsProtectedSystemRole)
                {
                    return RoleOperationResult.AuditedFailure("role.system_role_protected", role.Id, AuditAction.RoleAdministrationRejected);
                }

                if (role.Status == RoleStatus.Archived)
                {
                    return RoleOperationResult.Failure("role.archived");
                }

                int assignmentCount = await ActiveAssignmentCountAsync(identity, roleId, token);
                if (assignmentCount > 0)
                {
                    return RoleOperationResult.AuditedFailure(
                        "role.has_active_assignments",
                        role.Id,
                        AuditAction.RoleAdministrationRejected,
                        assignmentCount);
                }

                role.Archive(now);
                await AppendAuditAsync(
                    audit, now, organizationId, actorId, AuditAction.RoleArchived, role.Id,
                    null, SafeJson(new { status = "archived" }), null, correlationId, token);
                return RoleOperationResult.Archived(role.Version);
            },
            cancellationToken);

    private async Task<RoleOperationResult> ExecuteAsync(
        Guid organizationId,
        Guid actorId,
        string correlationId,
        Func<IdentityDbContext, IAuditWriter, DateTimeOffset, CancellationToken, Task<RoleOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options;
        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options;
        await using var identity = new IdentityDbContext(identityOptions);
        await using var auditContext = new AuditDbContext(auditOptions);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await identity.Database.UseTransactionAsync(transaction, cancellationToken);
        await auditContext.Database.UseTransactionAsync(transaction, cancellationToken);

        RoleOperationResult result;
        try
        {
            result = await operation(identity, new AuditWriter(auditContext), _clock.UtcNow, cancellationToken);
            if (!result.Succeeded)
            {
                await RollbackIfActiveAsync(transaction);
                if (result.RejectionAuditAction is not null)
                {
                    await WriteRejectionAuditAsync(organizationId, actorId, result, correlationId, cancellationToken);
                }

                return result;
            }

            await identity.SaveChangesAsync(cancellationToken);
            await auditContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (FindPostgresException(exception) is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_roles_organization_id_normalized_name" })
        {
            await RollbackIfActiveAsync(transaction);
            return RoleOperationResult.Failure("role.name_conflict");
        }
        catch (Exception exception) when (FindPostgresException(exception) is { SqlState: PostgresErrorCodes.CheckViolation, ConstraintName: LastManagementAccessConstraint })
        {
            await RollbackIfActiveAsync(transaction);
            result = RoleOperationResult.AuditedFailure(
                "role.last_management_access",
                null,
                AuditAction.LastManagementAccessProtectionTriggered);
            await WriteRejectionAuditAsync(organizationId, actorId, result, correlationId, cancellationToken);
            return result;
        }
        catch
        {
            await RollbackIfActiveAsync(transaction);
            throw;
        }
    }

    private async Task WriteRejectionAuditAsync(
        Guid organizationId,
        Guid actorId,
        RoleOperationResult result,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options;
        await using var audit = new AuditDbContext(options);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await audit.Database.UseTransactionAsync(transaction, cancellationToken);
        try
        {
            var writer = new AuditWriter(audit);
            await AppendAuditAsync(
                writer,
                _clock.UtcNow,
                organizationId,
                actorId,
                result.RejectionAuditAction!.Value,
                result.EntityId,
                null,
                SafeJson(new { result.ErrorCode, result.ActiveAssignmentCount }),
                result.ErrorCode,
                correlationId,
                cancellationToken);
            await audit.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task RollbackIfActiveAsync(NpgsqlTransaction transaction)
    {
        if (transaction.Connection is not null)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // PostgreSQL has already completed the transaction after a deferred commit failure.
            }
        }
    }

    private static async Task<Permission[]?> ResolvePermissionsAsync(
        IdentityDbContext identity,
        IReadOnlyCollection<string> requestedCodes,
        CancellationToken cancellationToken)
    {
        Permission[] activePermissions = await identity.Permissions
            .Where(permission => permission.Status == PermissionStatus.Active)
            .OrderBy(permission => permission.Code)
            .ToArrayAsync(cancellationToken);
        string[] databaseCodes = activePermissions.Select(permission => permission.Code).ToArray();
        string[] compiledCodes = PermissionCatalogue.AllCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray();
        string[] presentationCodes = PermissionPresentationCatalogue.All.Select(entry => entry.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        if (!databaseCodes.SequenceEqual(compiledCodes, StringComparer.Ordinal)
            || !presentationCodes.SequenceEqual(compiledCodes, StringComparer.Ordinal))
        {
            return null;
        }

        if (requestedCodes.Any(code => !PermissionCatalogue.Contains(code) || !PermissionPresentationCatalogue.TryGet(code, out _)))
        {
            return [];
        }

        Dictionary<string, Permission> byCode = activePermissions.ToDictionary(permission => permission.Code, StringComparer.Ordinal);
        return requestedCodes.All(byCode.ContainsKey)
            ? requestedCodes.Select(code => byCode[code]).ToArray()
            : [];
    }

    private static RoleInputValidation ValidateInput(RoleWriteRequest request)
    {
        RoleName name;
        try
        {
            name = new RoleName(request.Name);
        }
        catch (ArgumentException)
        {
            return RoleInputValidation.Failure("role.validation_failed");
        }

        if (request.PermissionCodes is null || request.PermissionCodes.Count == 0)
        {
            return RoleInputValidation.Failure("role.permission_set_required");
        }

        if (request.PermissionCodes.Count > PermissionCatalogue.AllCodes.Count)
        {
            return RoleInputValidation.Failure("role.permission_set_invalid");
        }

        string[] codes = request.PermissionCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray();
        if (codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
        {
            return RoleInputValidation.Failure("role.permission_set_invalid");
        }

        try
        {
            foreach (string code in codes)
            {
                _ = new PermissionCode(code);
            }
        }
        catch (ArgumentException)
        {
            return RoleInputValidation.Failure("role.permission_set_invalid");
        }

        if (codes.Any(code => !PermissionCatalogue.Contains(code) || !PermissionPresentationCatalogue.TryGet(code, out _)))
        {
            return RoleInputValidation.Failure("role.permission_set_invalid");
        }

        return RoleInputValidation.Success(name, codes);
    }

    private static async Task AcquireManagementAccessLockAsync(IdentityDbContext identity, Guid organizationId, CancellationToken cancellationToken)
    {
        string lockKey = "kalm.identity.management-access:" + organizationId.ToString("D");
        await identity.Database.ExecuteSqlInterpolatedAsync(
            $"select pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }

    private static Task<int> ActiveAssignmentCountAsync(IdentityDbContext identity, Guid roleId, CancellationToken cancellationToken)
        => identity.UserRoleAssignments.CountAsync(
            assignment => assignment.RoleId == roleId && assignment.RevokedAtUtc == null,
            cancellationToken);

    private static RoleVersionedDetail ToDetail(Role role, IReadOnlyCollection<string> permissionCodes, int activeAssignmentCount)
        => new(
            new RoleDetailResponse(
                role.Id,
                role.Name,
                role.Status == RoleStatus.Active ? "active" : "archived",
                role.IsProtectedSystemRole,
                activeAssignmentCount,
                permissionCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray(),
                role.CreatedAtUtc,
                role.UpdatedAtUtc,
                role.ArchivedAtUtc),
            role.Version);

    private static Task AppendAuditAsync(
        IAuditWriter audit,
        DateTimeOffset now,
        Guid organizationId,
        Guid actorId,
        AuditAction action,
        Guid? roleId,
        string? beforeJson,
        string? afterJson,
        string? reasonCode,
        string correlationId,
        CancellationToken cancellationToken)
        => audit.AppendAsync(new AuditWriteRequest(
            Guid.NewGuid(), now, organizationId, null, null, actorId, AuditActorType.User, null,
            action, "Role", roleId, reasonCode is null ? AuditResult.Succeeded : AuditResult.Denied,
            reasonCode, correlationId, beforeJson, afterJson, null, null), cancellationToken);

    private static string SafeJson<T>(T value) => JsonSerializer.Serialize(value);

    private static PostgresException? FindPostgresException(Exception exception)
        => exception as PostgresException ?? exception.InnerException as PostgresException;
}

public sealed record RoleOperationResult(
    bool Succeeded,
    bool WasArchived,
    RoleVersionedDetail? Detail,
    string? ErrorCode,
    long? CurrentVersion,
    Guid? EntityId,
    AuditAction? RejectionAuditAction,
    int? ActiveAssignmentCount)
{
    public static RoleOperationResult Success(RoleVersionedDetail detail) => new(true, false, detail, null, null, detail.Role.Id, null, detail.Role.ActiveAssignmentCount);
    public static RoleOperationResult Archived(long version) => new(true, true, null, null, version, null, null, null);
    public static RoleOperationResult Failure(string errorCode, long? currentVersion = null) => new(false, false, null, errorCode, currentVersion, null, null, null);
    public static RoleOperationResult AuditedFailure(string errorCode, Guid? entityId, AuditAction action, int? activeAssignmentCount = null)
        => new(false, false, null, errorCode, null, entityId, action, activeAssignmentCount);
}

internal sealed record RoleInputValidation(bool Succeeded, RoleName? Name, string[] PermissionCodes, string? ErrorCode)
{
    public static RoleInputValidation Success(RoleName name, string[] codes) => new(true, name, codes, null);
    public static RoleInputValidation Failure(string code) => new(false, null, [], code);
}
