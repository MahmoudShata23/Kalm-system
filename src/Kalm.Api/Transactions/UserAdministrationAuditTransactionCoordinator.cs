using System.Data;
using System.Text.Json;
using Kalm.Api.Configuration;
using Kalm.Api.Features.UserAdministration;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity;
using Kalm.Identity.Application.ManagementAuthentication;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.Transactions;

public sealed class UserAdministrationAuditTransactionCoordinator
{
    private const string LastManagementAccessConstraint = "ck_identity_last_management_access";
    private readonly string _connectionString;
    private readonly IClock _clock;
    private readonly IPasswordHasher _passwordHasher;

    public UserAdministrationAuditTransactionCoordinator(
        IOptions<DatabaseOptions> database,
        IClock clock,
        IPasswordHasher passwordHasher)
    {
        _connectionString = database.Value.ConnectionString;
        _clock = clock;
        _passwordHasher = passwordHasher;
    }

    public Task<UserOperationResult> CreateAsync(
        Guid organizationId,
        Guid actorId,
        UserCreateRequest request,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            organizationId,
            actorId,
            correlationId,
            async (identity, organization, audit, now, token) =>
            {
                UserInputValidation validation = Validate(
                    request.Username,
                    request.Email,
                    request.DisplayName,
                    request.PreferredLanguage,
                    request.RoleIds,
                    request.BranchAccessScope,
                    request.BranchIds);
                if (!validation.Succeeded)
                {
                    return UserOperationResult.Failure(validation.ErrorCode!);
                }

                string? encodedHash = null;
                if (request.InitialPassword is not null)
                {
                    try
                    {
                        encodedHash = _passwordHasher.Hash(request.InitialPassword);
                    }
                    catch (ArgumentException)
                    {
                        return UserOperationResult.Failure("user.password_invalid");
                    }
                }

                if (await identity.Users.AnyAsync(
                    user => user.NormalizedUsername == validation.Username!.NormalizedValue
                        || (validation.Email != null && user.NormalizedEmail == validation.Email.NormalizedValue),
                    token))
                {
                    return UserOperationResult.Failure("user.identifier_conflict");
                }

                Role[]? roles = await ResolveActiveRolesAsync(identity, organizationId, validation.RoleIds, token);
                if (roles is null)
                {
                    return UserOperationResult.Failure("user.roles_invalid");
                }

                await BranchMutationLock.AcquireAsync(organization, organizationId, validation.BranchIds, token);
                Branch[]? branches = await ResolveActiveBranchesAsync(organization, organizationId, validation.BranchIds, token);
                if (branches is null)
                {
                    return UserOperationResult.Failure("user.branch_access_invalid");
                }

                var user = User.Create(
                    Guid.NewGuid(), organizationId, validation.Username!, validation.Email,
                    validation.DisplayName!, validation.PreferredLanguage!, now);
                var credential = PasswordCredential.Create(Guid.NewGuid(), user.Id, now);
                if (encodedHash is not null)
                {
                    credential.CompleteSetup(encodedHash, now);
                }

                identity.Users.Add(user);
                identity.PasswordCredentials.Add(credential);
                foreach (Role role in roles)
                {
                    identity.UserRoleAssignments.Add(UserRoleAssignment.Assign(Guid.NewGuid(), organizationId, user.Id, role.Id, now));
                }

                var access = UserBranchAccess.Create(Guid.NewGuid(), organizationId, user.Id, validation.Scope, now);
                organization.UserBranchAccesses.Add(access);
                foreach (Branch branch in branches)
                {
                    organization.UserBranchAssignments.Add(UserBranchAssignment.Assign(Guid.NewGuid(), access.Id, organizationId, branch.Id, now));
                }

                await AppendAuditAsync(
                    audit, now, organizationId, actorId, AuditAction.UserCreated, user.Id,
                    null,
                    SafeJson(new
                    {
                        user.Username,
                        user.DisplayName,
                        user.PreferredLanguage,
                        roleIds = validation.RoleIds,
                        branchAccessScope = UserAdministrationQueries.Scope(validation.Scope),
                        branchIds = validation.BranchIds,
                        credentialStatus = encodedHash is null ? "pendingSetup" : "active"
                    }),
                    null, correlationId, token);
                if (encodedHash is not null)
                {
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.UserPasswordSet, user.Id,
                        null, SafeJson(new { credentialStatus = "active", sessionsRevoked = 0 }),
                        null, correlationId, token);
                }

                return UserOperationResult.Success(ToDetail(user, credential, validation.RoleIds, validation.Scope, validation.BranchIds));
            },
            cancellationToken);

    public Task<UserOperationResult> UpdateAsync(
        Guid organizationId,
        Guid actorId,
        Guid userId,
        long expectedVersion,
        UserUpdateRequest request,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            organizationId,
            actorId,
            correlationId,
            async (identity, organization, audit, now, token) =>
            {
                UserInputValidation validation = Validate(
                    request.Username, request.Email, request.DisplayName, request.PreferredLanguage,
                    request.RoleIds, request.BranchAccessScope, request.BranchIds);
                if (!validation.Succeeded)
                {
                    return UserOperationResult.Failure(validation.ErrorCode!);
                }

                await AcquireManagementAccessLockAsync(identity, organizationId, token);
                User? user = await LockUserAsync(identity, organizationId, userId, token);
                if (user is null)
                {
                    return UserOperationResult.Failure("user.not_found");
                }

                if (user.Version != expectedVersion)
                {
                    return UserOperationResult.Failure("user.concurrency_conflict", user.Version);
                }

                if (user.Status == UserStatus.Archived)
                {
                    return UserOperationResult.Failure("user.archived");
                }

                if (await identity.Users.AnyAsync(
                    candidate => candidate.Id != userId
                        && (candidate.NormalizedUsername == validation.Username!.NormalizedValue
                            || (validation.Email != null && candidate.NormalizedEmail == validation.Email.NormalizedValue)),
                    token))
                {
                    return UserOperationResult.Failure("user.identifier_conflict");
                }

                Role[]? roles = await ResolveActiveRolesAsync(identity, organizationId, validation.RoleIds, token);
                if (roles is null)
                {
                    return UserOperationResult.Failure("user.roles_invalid");
                }

                await BranchMutationLock.AcquireAsync(organization, organizationId, validation.BranchIds, token);
                Branch[]? branches = await ResolveActiveBranchesAsync(organization, organizationId, validation.BranchIds, token);
                if (branches is null)
                {
                    return UserOperationResult.Failure("user.branch_access_invalid");
                }

                UserBranchAccess? access = await organization.UserBranchAccesses
                    .FromSqlInterpolated($"select * from organization.user_branch_access where user_id = {userId} and organization_id = {organizationId} for update")
                    .SingleOrDefaultAsync(token);
                if (access is null)
                {
                    return UserOperationResult.Failure("user.not_found");
                }

                UserRoleAssignment[] currentAssignments = await identity.UserRoleAssignments
                    .Where(assignment => assignment.UserId == userId && assignment.OrganizationId == organizationId && assignment.RevokedAtUtc == null)
                    .ToArrayAsync(token);
                Guid[] currentRoleIds = currentAssignments.Select(assignment => assignment.RoleId).OrderBy(id => id).ToArray();
                Guid[] addedRoleIds = validation.RoleIds.Except(currentRoleIds).OrderBy(id => id).ToArray();
                Guid[] removedRoleIds = currentRoleIds.Except(validation.RoleIds).OrderBy(id => id).ToArray();

                UserBranchAssignment[] currentBranchAssignments = await organization.UserBranchAssignments
                    .Where(assignment => assignment.AccessId == access.Id && assignment.RevokedAtUtc == null)
                    .ToArrayAsync(token);
                Guid[] currentBranchIds = currentBranchAssignments.Select(assignment => assignment.BranchId).OrderBy(id => id).ToArray();
                BranchAccessScope currentScope = access.Scope;
                Guid[] addedBranchIds = validation.BranchIds.Except(currentBranchIds).OrderBy(id => id).ToArray();
                Guid[] removedBranchIds = currentBranchIds.Except(validation.BranchIds).OrderBy(id => id).ToArray();
                bool scopeChanged = access.Scope != validation.Scope;
                bool roleChanged = addedRoleIds.Length > 0 || removedRoleIds.Length > 0;
                bool branchChanged = scopeChanged || addedBranchIds.Length > 0 || removedBranchIds.Length > 0;
                bool authorizationChanged = roleChanged || branchChanged;

                var oldProfile = new
                {
                    user.Username,
                    user.Email,
                    user.DisplayName,
                    user.PreferredLanguage
                };
                bool changed = user.UpdateProfile(
                    validation.Username!, validation.Email, validation.DisplayName!, validation.PreferredLanguage!, authorizationChanged, now);
                if (!changed)
                {
                    PasswordCredential unchangedCredential = await identity.PasswordCredentials.SingleAsync(candidate => candidate.UserId == userId, token);
                    return UserOperationResult.Success(ToDetail(user, unchangedCredential, currentRoleIds, access.Scope, currentBranchIds));
                }

                HashSet<Guid> removedRoles = removedRoleIds.ToHashSet();
                foreach (UserRoleAssignment assignment in currentAssignments.Where(assignment => removedRoles.Contains(assignment.RoleId)))
                {
                    assignment.Revoke(now);
                }

                foreach (Guid roleId in addedRoleIds)
                {
                    identity.UserRoleAssignments.Add(UserRoleAssignment.Assign(Guid.NewGuid(), organizationId, userId, roleId, now));
                }

                access.ChangeScope(validation.Scope, now);
                HashSet<Guid> removedBranches = removedBranchIds.ToHashSet();
                foreach (UserBranchAssignment assignment in currentBranchAssignments.Where(assignment => removedBranches.Contains(assignment.BranchId)))
                {
                    assignment.Revoke(now);
                }

                foreach (Guid branchId in addedBranchIds)
                {
                    organization.UserBranchAssignments.Add(UserBranchAssignment.Assign(Guid.NewGuid(), access.Id, organizationId, branchId, now));
                }

                bool profileChanged = !string.Equals(oldProfile.Username, user.Username, StringComparison.Ordinal)
                    || !string.Equals(oldProfile.Email, user.Email, StringComparison.Ordinal)
                    || !string.Equals(oldProfile.DisplayName, user.DisplayName, StringComparison.Ordinal)
                    || !string.Equals(oldProfile.PreferredLanguage, user.PreferredLanguage, StringComparison.Ordinal);
                if (profileChanged)
                {
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.UserProfileChanged, user.Id,
                        SafeJson(new
                        {
                            oldProfile.Username,
                            oldProfile.DisplayName,
                            oldProfile.PreferredLanguage,
                            emailPresent = oldProfile.Email is not null
                        }),
                        SafeJson(new
                        {
                            user.Username,
                            user.DisplayName,
                            user.PreferredLanguage,
                            emailPresent = user.Email is not null,
                            emailChanged = !string.Equals(oldProfile.Email, user.Email, StringComparison.Ordinal)
                        }),
                        null, correlationId, token);
                }

                if (addedRoleIds.Length > 0)
                {
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.UserRoleAssigned, user.Id,
                        null, SafeJson(new { roleIds = addedRoleIds }), null, correlationId, token);
                }

                if (removedRoleIds.Length > 0)
                {
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.UserRoleRevoked, user.Id,
                        SafeJson(new { roleIds = removedRoleIds }), null, null, correlationId, token);
                }

                if (branchChanged)
                {
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.UserBranchAccessChanged, user.Id,
                        SafeJson(new { branchAccessScope = UserAdministrationQueries.Scope(currentScope), branchIds = currentBranchIds }),
                        SafeJson(new
                        {
                            branchAccessScope = UserAdministrationQueries.Scope(validation.Scope),
                            branchIds = validation.BranchIds,
                            addedBranchIds,
                            removedBranchIds
                        }),
                        null, correlationId, token);
                }

                PasswordCredential credential = await identity.PasswordCredentials.SingleAsync(candidate => candidate.UserId == userId, token);
                return UserOperationResult.Success(ToDetail(user, credential, validation.RoleIds, validation.Scope, validation.BranchIds));
            },
            cancellationToken);

    public Task<UserOperationResult> ActivateAsync(
        Guid organizationId,
        Guid actorId,
        Guid userId,
        long expectedVersion,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            organizationId,
            actorId,
            correlationId,
            async (identity, organization, audit, now, token) =>
            {
                await AcquireManagementAccessLockAsync(identity, organizationId, token);
                User? user = await LockUserAsync(identity, organizationId, userId, token);
                if (user is null)
                {
                    return UserOperationResult.Failure("user.not_found");
                }

                if (user.Version != expectedVersion)
                {
                    return UserOperationResult.Failure("user.concurrency_conflict", user.Version);
                }

                if (user.Status == UserStatus.Archived)
                {
                    return UserOperationResult.Failure("user.archived");
                }

                PasswordCredential credential = await identity.PasswordCredentials.SingleAsync(candidate => candidate.UserId == userId, token);
                Guid[] roleIds = await ActiveRoleIdsAsync(identity, organizationId, userId, token);
                (UserBranchAccess? Access, Guid[] BranchIds, bool IsValid) branch = await ResolveBranchAccessAsync(organization, organizationId, userId, token);
                if (credential.Status != PasswordCredentialStatus.Active || roleIds.Length == 0 || !branch.IsValid || branch.Access is null)
                {
                    return UserOperationResult.AuditedFailure("user.activation_requirements_not_met", user.Id, AuditAction.UserAdministrationRejected);
                }

                if (user.Status != UserStatus.Active)
                {
                    user.Activate(credential, now);
                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.UserActivated, user.Id,
                        SafeJson(new { status = "suspended" }), SafeJson(new { status = "active" }),
                        null, correlationId, token);
                }

                return UserOperationResult.Success(ToDetail(user, credential, roleIds, branch.Access.Scope, branch.BranchIds));
            },
            cancellationToken);

    public Task<UserOperationResult> SuspendAsync(
        Guid organizationId,
        Guid actorId,
        Guid userId,
        long expectedVersion,
        bool confirmSelfSuspension,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            organizationId,
            actorId,
            correlationId,
            async (identity, organization, audit, now, token) =>
            {
                await AcquireManagementAccessLockAsync(identity, organizationId, token);
                User? user = await LockUserAsync(identity, organizationId, userId, token);
                if (user is null)
                {
                    return UserOperationResult.Failure("user.not_found");
                }

                if (user.Version != expectedVersion)
                {
                    return UserOperationResult.Failure("user.concurrency_conflict", user.Version);
                }

                if (user.Status == UserStatus.Archived)
                {
                    return UserOperationResult.Failure("user.archived");
                }

                if (userId == actorId && !confirmSelfSuspension)
                {
                    return UserOperationResult.AuditedFailure("user.self_suspension_confirmation_required", user.Id, AuditAction.UserAdministrationRejected);
                }

                if (user.Suspend(now))
                {
                    UserSession[] sessions = await identity.UserSessions
                        .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
                        .ToArrayAsync(token);
                    foreach (UserSession session in sessions)
                    {
                        session.Revoke(SessionRevocationReason.UserSuspended, now);
                    }

                    await AppendAuditAsync(
                        audit, now, organizationId, actorId, AuditAction.UserSuspended, user.Id,
                        SafeJson(new { status = "active" }),
                        SafeJson(new { status = "suspended", sessionsRevoked = sessions.Length }),
                        null, correlationId, token);
                }

                PasswordCredential credential = await identity.PasswordCredentials.SingleAsync(candidate => candidate.UserId == userId, token);
                Guid[] roleIds = await CurrentRoleIdsAsync(identity, organizationId, userId, token);
                (UserBranchAccess? Access, Guid[] BranchIds, _) = await ResolveBranchAccessAsync(organization, organizationId, userId, token);
                return UserOperationResult.Success(ToDetail(user, credential, roleIds, Access!, BranchIds));
            },
            cancellationToken);

    public Task<UserOperationResult> SetPasswordAsync(
        Guid organizationId,
        Guid actorId,
        Guid userId,
        long expectedVersion,
        string password,
        string correlationId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            organizationId,
            actorId,
            correlationId,
            async (identity, organization, audit, now, token) =>
            {
                string encodedHash;
                try
                {
                    encodedHash = _passwordHasher.Hash(password);
                }
                catch (ArgumentException)
                {
                    return UserOperationResult.Failure("user.password_invalid");
                }

                User? user = await LockUserAsync(identity, organizationId, userId, token);
                if (user is null)
                {
                    return UserOperationResult.Failure("user.not_found");
                }

                if (user.Version != expectedVersion)
                {
                    return UserOperationResult.Failure("user.concurrency_conflict", user.Version);
                }

                if (user.Status == UserStatus.Archived)
                {
                    return UserOperationResult.Failure("user.archived");
                }

                PasswordCredential credential = await identity.PasswordCredentials
                    .FromSqlInterpolated($"select * from identity.password_credentials where user_id = {userId} for update")
                    .SingleAsync(token);
                bool initialSetup = credential.Status == PasswordCredentialStatus.PendingSetup;
                if (initialSetup)
                {
                    credential.CompleteSetup(encodedHash, now);
                }
                else
                {
                    credential.ReplaceHash(encodedHash, now);
                    credential.ClearFailures(now);
                }

                UserSession[] sessions = await identity.UserSessions
                    .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
                    .ToArrayAsync(token);
                foreach (UserSession session in sessions)
                {
                    session.Revoke(SessionRevocationReason.CredentialChanged, now);
                }

                user.RecordCredentialChange(now);
                await AppendAuditAsync(
                    audit, now, organizationId, actorId,
                    initialSetup ? AuditAction.UserPasswordSet : AuditAction.UserPasswordReset,
                    user.Id, null,
                    SafeJson(new { credentialStatus = "active", sessionsRevoked = sessions.Length }),
                    null, correlationId, token);

                Guid[] roleIds = await CurrentRoleIdsAsync(identity, organizationId, userId, token);
                (UserBranchAccess? Access, Guid[] BranchIds, _) = await ResolveBranchAccessAsync(organization, organizationId, userId, token);
                return UserOperationResult.Success(ToDetail(user, credential, roleIds, Access!, BranchIds));
            },
            cancellationToken);

    private async Task<UserOperationResult> ExecuteAsync(
        Guid organizationId,
        Guid actorId,
        string correlationId,
        Func<IdentityDbContext, OrganizationDbContext, IAuditWriter, DateTimeOffset, CancellationToken, Task<UserOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options;
        var organizationOptions = new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(connection).Options;
        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options;
        await using var identity = new IdentityDbContext(identityOptions);
        await using var organization = new OrganizationDbContext(organizationOptions);
        await using var auditContext = new AuditDbContext(auditOptions);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await identity.Database.UseTransactionAsync(transaction, cancellationToken);
        await organization.Database.UseTransactionAsync(transaction, cancellationToken);
        await auditContext.Database.UseTransactionAsync(transaction, cancellationToken);

        try
        {
            UserOperationResult result = await operation(identity, organization, new AuditWriter(auditContext), _clock.UtcNow, cancellationToken);
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
            await organization.SaveChangesAsync(cancellationToken);
            await auditContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (FindPostgresException(exception) is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_users_normalized_username" or "ux_users_normalized_email" })
        {
            await RollbackIfActiveAsync(transaction);
            return UserOperationResult.Failure("user.identifier_conflict");
        }
        catch (Exception exception) when (FindPostgresException(exception) is { SqlState: PostgresErrorCodes.CheckViolation, ConstraintName: LastManagementAccessConstraint })
        {
            await RollbackIfActiveAsync(transaction);
            UserOperationResult result = UserOperationResult.AuditedFailure(
                "user.last_management_access", null, AuditAction.LastManagementAccessProtectionTriggered);
            await WriteRejectionAuditAsync(organizationId, actorId, result, correlationId, cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackIfActiveAsync(transaction);
            return UserOperationResult.Failure("user.concurrency_conflict");
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
        UserOperationResult result,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connection).Options;
        await using var audit = new AuditDbContext(options);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await audit.Database.UseTransactionAsync(transaction, cancellationToken);
        var writer = new AuditWriter(audit);
        await AppendAuditAsync(
            writer, _clock.UtcNow, organizationId, actorId, result.RejectionAuditAction!.Value,
            result.EntityId, null, SafeJson(new { result.ErrorCode }), result.ErrorCode,
            correlationId, cancellationToken);
        await audit.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static UserInputValidation Validate(
        string usernameValue,
        string? emailValue,
        string displayNameValue,
        string preferredLanguage,
        IReadOnlyCollection<Guid>? roleIds,
        string scopeValue,
        IReadOnlyCollection<Guid>? branchIds)
    {
        Username username;
        EmailAddress? email;
        DisplayName displayName;
        try
        {
            username = new Username(usernameValue);
            email = string.IsNullOrWhiteSpace(emailValue) ? null : new EmailAddress(emailValue);
            displayName = new DisplayName(displayNameValue);
        }
        catch (ArgumentException)
        {
            return UserInputValidation.Failure("user.validation_failed");
        }

        string language = (preferredLanguage ?? string.Empty).Trim().ToLowerInvariant();
        if (language is not ("en" or "ar") || roleIds is null || roleIds.Count is < 1 or > 100
            || roleIds.Any(id => id == Guid.Empty) || roleIds.Distinct().Count() != roleIds.Count
            || branchIds is null || branchIds.Count > 100 || branchIds.Any(id => id == Guid.Empty)
            || branchIds.Distinct().Count() != branchIds.Count)
        {
            return UserInputValidation.Failure("user.validation_failed");
        }

        BranchAccessScope scope = scopeValue switch
        {
            "assignedBranches" => BranchAccessScope.AssignedBranches,
            "allOrganizationBranches" => BranchAccessScope.AllOrganizationBranches,
            _ => (BranchAccessScope)(-1)
        };
        if (scope == (BranchAccessScope)(-1)
            || (scope == BranchAccessScope.AssignedBranches && branchIds.Count == 0)
            || (scope == BranchAccessScope.AllOrganizationBranches && branchIds.Count != 0))
        {
            return UserInputValidation.Failure("user.branch_access_invalid");
        }

        return UserInputValidation.Success(
            username,
            email,
            displayName,
            language,
            roleIds.OrderBy(id => id).ToArray(),
            scope,
            branchIds.OrderBy(id => id).ToArray());
    }

    private static async Task<Role[]?> ResolveActiveRolesAsync(
        IdentityDbContext identity,
        Guid organizationId,
        Guid[] roleIds,
        CancellationToken cancellationToken)
    {
        Role[] roles = await identity.Roles
            .Where(role => roleIds.Contains(role.Id) && role.OrganizationId == organizationId && role.Status == RoleStatus.Active)
            .OrderBy(role => role.Id)
            .ToArrayAsync(cancellationToken);
        return roles.Length == roleIds.Length ? roles : null;
    }

    private static async Task<Branch[]?> ResolveActiveBranchesAsync(
        OrganizationDbContext organization,
        Guid organizationId,
        Guid[] branchIds,
        CancellationToken cancellationToken)
    {
        if (branchIds.Length == 0)
        {
            return [];
        }

        Branch[] branches = await organization.Branches
            .Where(branch => branchIds.Contains(branch.Id) && branch.OrganizationId == organizationId && branch.Status == BranchStatus.Active)
            .OrderBy(branch => branch.Id)
            .ToArrayAsync(cancellationToken);
        return branches.Length == branchIds.Length ? branches : null;
    }

    private static Task<User?> LockUserAsync(IdentityDbContext identity, Guid organizationId, Guid userId, CancellationToken cancellationToken)
        => identity.Users
            .FromSqlInterpolated($"select * from identity.users where id = {userId} and organization_id = {organizationId} for update")
            .SingleOrDefaultAsync(cancellationToken);

    private static Task<Guid[]> CurrentRoleIdsAsync(IdentityDbContext identity, Guid organizationId, Guid userId, CancellationToken cancellationToken)
        => identity.UserRoleAssignments
            .Where(assignment => assignment.UserId == userId && assignment.OrganizationId == organizationId && assignment.RevokedAtUtc == null)
            .OrderBy(assignment => assignment.RoleId)
            .Select(assignment => assignment.RoleId)
            .ToArrayAsync(cancellationToken);

    private static Task<Guid[]> ActiveRoleIdsAsync(IdentityDbContext identity, Guid organizationId, Guid userId, CancellationToken cancellationToken)
        => (from assignment in identity.UserRoleAssignments
            join role in identity.Roles on assignment.RoleId equals role.Id
            where assignment.UserId == userId && assignment.OrganizationId == organizationId
                && assignment.RevokedAtUtc == null && role.Status == RoleStatus.Active
            orderby role.Id
            select role.Id).ToArrayAsync(cancellationToken);

    private static async Task<(UserBranchAccess? Access, Guid[] BranchIds, bool IsValid)> ResolveBranchAccessAsync(
        OrganizationDbContext organization,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        UserBranchAccess? access = await organization.UserBranchAccesses
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.OrganizationId == organizationId, cancellationToken);
        if (access is null)
        {
            return (null, [], false);
        }

        Guid[] branchIds = await organization.UserBranchAssignments
            .Where(assignment => assignment.AccessId == access.Id && assignment.RevokedAtUtc == null)
            .OrderBy(assignment => assignment.BranchId)
            .Select(assignment => assignment.BranchId)
            .ToArrayAsync(cancellationToken);
        bool valid = access.Scope == BranchAccessScope.AllOrganizationBranches
            ? branchIds.Length == 0
            : branchIds.Length > 0 && await organization.Branches.CountAsync(
                branch => branch.OrganizationId == organizationId && branch.Status == BranchStatus.Active && branchIds.Contains(branch.Id),
                cancellationToken) == branchIds.Length;
        return (access, branchIds, valid);
    }

    private static async Task AcquireManagementAccessLockAsync(IdentityDbContext identity, Guid organizationId, CancellationToken cancellationToken)
    {
        string lockKey = "kalm.identity.management-access:" + organizationId.ToString("D");
        await identity.Database.ExecuteSqlInterpolatedAsync(
            $"select pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }

    private static UserVersionedDetail ToDetail(
        User user,
        PasswordCredential credential,
        IReadOnlyCollection<Guid> roleIds,
        UserBranchAccess access,
        IReadOnlyCollection<Guid> branchIds)
        => ToDetail(user, credential, roleIds, access.Scope, branchIds);

    private static UserVersionedDetail ToDetail(
        User user,
        PasswordCredential credential,
        IReadOnlyCollection<Guid> roleIds,
        BranchAccessScope scope,
        IReadOnlyCollection<Guid> branchIds)
        => new(
            new UserDetailResponse(
                user.Id,
                user.Username,
                user.Email,
                user.DisplayName,
                user.PreferredLanguage,
                UserAdministrationQueries.Status(user.Status),
                credential.Status == PasswordCredentialStatus.Active ? "active" : "pendingSetup",
                roleIds.OrderBy(id => id).ToArray(),
                UserAdministrationQueries.Scope(scope),
                branchIds.OrderBy(id => id).ToArray(),
                user.CreatedAtUtc,
                user.UpdatedAtUtc,
                user.ActivatedAtUtc),
            user.Version);

    private static Task AppendAuditAsync(
        IAuditWriter audit,
        DateTimeOffset now,
        Guid organizationId,
        Guid actorId,
        AuditAction action,
        Guid? userId,
        string? beforeJson,
        string? afterJson,
        string? reasonCode,
        string correlationId,
        CancellationToken cancellationToken)
        => audit.AppendAsync(new AuditWriteRequest(
            Guid.NewGuid(), now, organizationId, null, null, actorId, AuditActorType.User, null,
            action, "User", userId, reasonCode is null ? AuditResult.Succeeded : AuditResult.Denied,
            reasonCode, correlationId, beforeJson, afterJson, null, null), cancellationToken);

    private static string SafeJson<T>(T value) => JsonSerializer.Serialize(value);

    private static PostgresException? FindPostgresException(Exception exception)
        => exception as PostgresException ?? exception.InnerException as PostgresException;

    private static async Task RollbackIfActiveAsync(NpgsqlTransaction transaction)
    {
        if (transaction.Connection is null)
        {
            return;
        }

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

public sealed record UserOperationResult(
    bool Succeeded,
    UserVersionedDetail? Detail,
    string? ErrorCode,
    long? CurrentVersion,
    Guid? EntityId,
    AuditAction? RejectionAuditAction)
{
    public static UserOperationResult Success(UserVersionedDetail detail) => new(true, detail, null, null, detail.User.Id, null);
    public static UserOperationResult Failure(string errorCode, long? currentVersion = null) => new(false, null, errorCode, currentVersion, null, null);
    public static UserOperationResult AuditedFailure(string errorCode, Guid? entityId, AuditAction action)
        => new(false, null, errorCode, null, entityId, action);
}

internal sealed record UserInputValidation(
    bool Succeeded,
    Username? Username,
    EmailAddress? Email,
    DisplayName? DisplayName,
    string? PreferredLanguage,
    Guid[] RoleIds,
    BranchAccessScope Scope,
    Guid[] BranchIds,
    string? ErrorCode)
{
    public static UserInputValidation Success(
        Username username,
        EmailAddress? email,
        DisplayName displayName,
        string preferredLanguage,
        Guid[] roleIds,
        BranchAccessScope scope,
        Guid[] branchIds)
        => new(true, username, email, displayName, preferredLanguage, roleIds, scope, branchIds, null);

    public static UserInputValidation Failure(string errorCode)
        => new(false, null, null, null, null, [], BranchAccessScope.AssignedBranches, [], errorCode);
}
