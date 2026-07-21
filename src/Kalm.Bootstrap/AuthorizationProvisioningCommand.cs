using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Authorization;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kalm.Bootstrap;

internal static class AuthorizationProvisioningCommand
{
    private const long ProvisioningAdvisoryLock = 4_879_113_202_607_21;

    public static async Task ProvisionAsync(
        IdentityDbContext identity,
        OrganizationDbContext organization,
        IAuditWriter audit,
        User user,
        PasswordCredential credential,
        BranchAccessScope scope,
        IReadOnlyCollection<Guid> branchIds,
        DateTimeOffset now,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (user.Status != UserStatus.Active || credential.UserId != user.Id || credential.Status != PasswordCredentialStatus.Active)
        {
            throw new AuthorizationProvisioningConflictException("target_ineligible");
        }

        if (scope == BranchAccessScope.AssignedBranches && branchIds.Count == 0)
        {
            throw new AuthorizationProvisioningConflictException("assigned_scope_empty");
        }

        if (scope == BranchAccessScope.AllOrganizationBranches && branchIds.Count != 0)
        {
            throw new AuthorizationProvisioningConflictException("all_scope_has_assignments");
        }

        await identity.Database.ExecuteSqlRawAsync($"select pg_advisory_xact_lock({ProvisioningAdvisoryLock})", cancellationToken);

        Permission[] permissions = await identity.Permissions
            .Where(permission => permission.Status == PermissionStatus.Active)
            .OrderBy(permission => permission.Code)
            .ToArrayAsync(cancellationToken);
        string[] expectedCodes = PermissionCatalogue.FirstAdministratorPermissionCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray();
        Permission[] approvedPermissions = permissions.Where(permission => PermissionCatalogue.Contains(permission.Code)).ToArray();
        if (!approvedPermissions.Select(permission => permission.Code).SequenceEqual(expectedCodes, StringComparer.Ordinal))
        {
            throw new AuthorizationProvisioningConflictException("permission_catalogue_mismatch");
        }

        Role? role = await identity.Roles
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == user.OrganizationId
                && candidate.SystemKey == PermissionCatalogue.FirstAdministratorSystemRoleKey, cancellationToken);
        bool changed = false;
        if (role is null)
        {
            role = Role.Create(
                Guid.NewGuid(), user.OrganizationId, new RoleName("Initial Administrator"),
                PermissionCatalogue.FirstAdministratorSystemRoleKey, now);
            identity.Roles.Add(role);
            changed = true;
            await AppendAsync(
                audit, now, user.OrganizationId, AuditAction.SystemRoleProvisioned, "Role", role.Id,
                null,
                SafeJson(("systemKey", PermissionCatalogue.FirstAdministratorSystemRoleKey)),
                correlationId, cancellationToken);
        }
        else if (role.Status != RoleStatus.Active)
        {
            throw new AuthorizationProvisioningConflictException("system_role_inactive");
        }

        RolePermission[] activeGrants = await identity.RolePermissions
            .Where(grant => grant.RoleId == role.Id && grant.RevokedAtUtc == null)
            .ToArrayAsync(cancellationToken);
        if (activeGrants.Length == 0)
        {
            foreach (Permission permission in approvedPermissions)
            {
                identity.RolePermissions.Add(RolePermission.Grant(Guid.NewGuid(), role.Id, permission.Id, now));
            }

            role.RecordPermissionSetChanged(now);
            changed = true;
            await AppendAsync(
                audit, now, user.OrganizationId, AuditAction.RolePermissionSetChanged, "Role", role.Id,
                null,
                SafeJson(
                    ("permissionSetVersion", PermissionCatalogue.FirstAdministratorPermissionSetVersion),
                    ("permissionCodes", string.Join(',', expectedCodes))),
                correlationId, cancellationToken);
        }
        else
        {
            Guid[] activePermissionIds = activeGrants.Select(grant => grant.PermissionId).OrderBy(id => id).ToArray();
            Guid[] expectedPermissionIds = approvedPermissions.Select(permission => permission.Id).OrderBy(id => id).ToArray();
            if (!activePermissionIds.SequenceEqual(expectedPermissionIds))
            {
                throw new AuthorizationProvisioningConflictException("system_role_permission_conflict");
            }
        }

        UserRoleAssignment? assignment = await identity.UserRoleAssignments
            .SingleOrDefaultAsync(candidate => candidate.RoleId == role.Id && candidate.RevokedAtUtc == null, cancellationToken);
        if (assignment is null)
        {
            identity.UserRoleAssignments.Add(UserRoleAssignment.Assign(Guid.NewGuid(), user.OrganizationId, user.Id, role.Id, now));
            changed = true;
            await AppendAsync(
                audit, now, user.OrganizationId, AuditAction.UserRoleAssigned, "User", user.Id,
                null, SafeJson(("roleId", role.Id.ToString("D"))), correlationId, cancellationToken);
        }
        else if (assignment.UserId != user.Id || assignment.OrganizationId != user.OrganizationId)
        {
            throw new AuthorizationProvisioningConflictException("administrator_target_conflict");
        }

        UserBranchAccess? access = await organization.UserBranchAccesses
            .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken);
        Guid[] requestedBranchIds = branchIds.Distinct().OrderBy(id => id).ToArray();
        if (access is null)
        {
            access = UserBranchAccess.Create(Guid.NewGuid(), user.OrganizationId, user.Id, scope, now);
            organization.UserBranchAccesses.Add(access);
            foreach (Guid branchId in requestedBranchIds)
            {
                organization.UserBranchAssignments.Add(UserBranchAssignment.Assign(Guid.NewGuid(), access.Id, user.OrganizationId, branchId, now));
            }

            changed = true;
            await AppendAsync(
                audit, now, user.OrganizationId, AuditAction.UserBranchAccessChanged, "User", user.Id,
                null,
                SafeJson(("scope", scope.ToString()), ("branchIds", string.Join(',', requestedBranchIds))),
                correlationId, cancellationToken);
        }
        else
        {
            Guid[] existingBranchIds = await organization.UserBranchAssignments
                .Where(candidate => candidate.AccessId == access.Id && candidate.RevokedAtUtc == null)
                .Select(candidate => candidate.BranchId)
                .OrderBy(id => id)
                .ToArrayAsync(cancellationToken);
            if (access.OrganizationId != user.OrganizationId
                || access.Scope != scope
                || !existingBranchIds.SequenceEqual(requestedBranchIds))
            {
                throw new AuthorizationProvisioningConflictException("branch_scope_conflict");
            }
        }

        if (changed)
        {
            user.AdvanceAuthorizationVersion(now);
        }

        await AppendAsync(
            audit, now, user.OrganizationId, AuditAction.AuthorizationProvisioningCompleted, "User", user.Id,
            null,
            SafeJson(
                ("permissionSetVersion", PermissionCatalogue.FirstAdministratorPermissionSetVersion),
                ("scope", scope.ToString()),
                ("branchIds", string.Join(',', requestedBranchIds))),
            correlationId, cancellationToken);
    }

    public static async Task WriteFailureAuditAsync(
        string connectionString,
        string reasonCode,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options;
        await using var context = new AuditDbContext(options);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await context.Database.UseTransactionAsync(transaction, cancellationToken);
        var writer = new AuditWriter(context);
        await writer.AppendAsync(new AuditWriteRequest(
            Guid.NewGuid(), DateTimeOffset.UtcNow, null, null, null, null, AuditActorType.System, null,
            AuditAction.AuthorizationProvisioningFailed, "AuthorizationProvisioning", null, AuditResult.Failed,
            reasonCode, correlationId, null, null, null, null), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static Task AppendAsync(
        IAuditWriter audit,
        DateTimeOffset now,
        Guid organizationId,
        AuditAction action,
        string entityType,
        Guid entityId,
        string? beforeJson,
        string? afterJson,
        string correlationId,
        CancellationToken cancellationToken)
        => audit.AppendAsync(new AuditWriteRequest(
            Guid.NewGuid(), now, organizationId, null, null, null, AuditActorType.System, null,
            action, entityType, entityId, AuditResult.Succeeded, null, correlationId,
            beforeJson, afterJson, null, null), cancellationToken);

    private static string? SafeJson(params (string Key, string? Value)[] values)
        => AuditRedactionPolicy.CreateJson(values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));
}

internal sealed class AuthorizationProvisioningConflictException(string reasonCode) : Exception
{
    public string ReasonCode { get; } = reasonCode;
}

internal sealed record AuthorizationProvisioningArguments(
    string Username,
    BranchAccessScope Scope,
    IReadOnlyCollection<string> BranchCodes)
{
    public static AuthorizationProvisioningArguments Parse(string[] args)
    {
        string? username = null;
        bool allBranches = false;
        var branchCodes = new List<string>();
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option == "--all-organization-branches")
            {
                if (allBranches)
                {
                    throw new ArgumentException("--all-organization-branches was supplied more than once.");
                }

                allBranches = true;
                continue;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '{option}' is invalid.");
            }

            string value = args[++index];
            if (option == "--username" && username is null)
            {
                username = value;
            }
            else if (option == "--branch-code")
            {
                branchCodes.Add(value);
            }
            else
            {
                throw new ArgumentException($"Option '{option}' is invalid or duplicated.");
            }
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("--username is required.");
        }

        if (allBranches == (branchCodes.Count > 0))
        {
            throw new ArgumentException("Select exactly one scope using --all-organization-branches or one or more --branch-code values.");
        }

        return new AuthorizationProvisioningArguments(
            username,
            allBranches ? BranchAccessScope.AllOrganizationBranches : BranchAccessScope.AssignedBranches,
            branchCodes.Select(code => new Kalm.Organization.Domain.ValueObjects.BranchCode(code).Value).Distinct(StringComparer.Ordinal).ToArray());
    }
}
