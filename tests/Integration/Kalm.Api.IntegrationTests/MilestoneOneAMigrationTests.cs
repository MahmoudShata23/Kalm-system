using Kalm.Api.Configuration;
using Kalm.Api.Persistence;
using Kalm.Api.Transactions;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Domain.ValueObjects;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Identity.Authorization;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Cryptography;

namespace Kalm.Api.IntegrationTests;

public sealed class MilestoneOneAMigrationTests
{
    private const string FoundationMigration = "20260715140000_InitialFoundation";
    private const string OrganizationMigration = "20260720181706_AddOrganizationFoundation";
    private const string AuditMigration = "20260720181822_AddAuditFoundation";
    private const string AuditAuthenticationMigration = "20260720202409_ExtendManagementAuthenticationAuditActions";
    private const string IdentityMigration = "20260720202353_AddManagementAuthentication";
    private const string IdentityAuthorizationMigration = "20260720233035_AddAuthorizationFoundation";
    private const string OrganizationAuthorizationMigration = "20260720233359_AddExplicitUserBranchAccess";
    private const string AuditAuthorizationMigration = "20260720233502_ExtendAuthorizationAuditActions";

    [Fact]
    public async Task CleanDatabase_AppliesAllContextsWithSeparateHistoryTablesAndAuditTrigger()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await ApplyAllMigrationsAsync(database.ConnectionString);

        Assert.Equal("organization.organizations", await ResolveRegClassAsync(database.ConnectionString, "organization.organizations"));
        Assert.Equal("organization.branches", await ResolveRegClassAsync(database.ConnectionString, "organization.branches"));
        Assert.Equal("audit.audit_logs", await ResolveRegClassAsync(database.ConnectionString, "audit.audit_logs"));
        Assert.Equal("\"__EFMigrationsHistory\"", await ResolveRegClassAsync(database.ConnectionString, "\"__EFMigrationsHistory\""));
        Assert.Equal("organization.__ef_migrations_history", await ResolveRegClassAsync(database.ConnectionString, "organization.__ef_migrations_history"));
        Assert.Equal("audit.__ef_migrations_history", await ResolveRegClassAsync(database.ConnectionString, "audit.__ef_migrations_history"));
        Assert.Equal("identity.__ef_migrations_history", await ResolveRegClassAsync(database.ConnectionString, "identity.__ef_migrations_history"));
        Assert.Equal("identity.users", await ResolveRegClassAsync(database.ConnectionString, "identity.users"));
        Assert.Equal("identity.password_credentials", await ResolveRegClassAsync(database.ConnectionString, "identity.password_credentials"));
        Assert.Equal("identity.user_sessions", await ResolveRegClassAsync(database.ConnectionString, "identity.user_sessions"));
        Assert.Equal("identity.login_attempts", await ResolveRegClassAsync(database.ConnectionString, "identity.login_attempts"));
        Assert.Equal("identity.permissions", await ResolveRegClassAsync(database.ConnectionString, "identity.permissions"));
        Assert.Equal("identity.roles", await ResolveRegClassAsync(database.ConnectionString, "identity.roles"));
        Assert.Equal("identity.role_permissions", await ResolveRegClassAsync(database.ConnectionString, "identity.role_permissions"));
        Assert.Equal("identity.user_role_assignments", await ResolveRegClassAsync(database.ConnectionString, "identity.user_role_assignments"));
        Assert.Equal("organization.user_branch_access", await ResolveRegClassAsync(database.ConnectionString, "organization.user_branch_access"));
        Assert.Equal("organization.user_branch_assignments", await ResolveRegClassAsync(database.ConnectionString, "organization.user_branch_assignments"));
        Assert.Equal("trg_audit_logs_immutable", await ResolveTriggerAsync(database.ConnectionString));
        Assert.Equal("organization.ux_organizations_singleton_key", await ResolveRegClassAsync(database.ConnectionString, "organization.ux_organizations_singleton_key"));
        Assert.Equal("organization.ux_branches_organization_id_code", await ResolveRegClassAsync(database.ConnectionString, "organization.ux_branches_organization_id_code"));
        Assert.Equal("audit.ix_audit_logs_entity_type_entity_id_occurred_at_utc", await ResolveRegClassAsync(database.ConnectionString, "audit.ix_audit_logs_entity_type_entity_id_occurred_at_utc"));
        Assert.True(await ConstraintExistsAsync(database.ConnectionString, "organization", "organizations", "ck_organizations_singleton_key"));
        Assert.True(await ConstraintExistsAsync(database.ConnectionString, "audit", "audit_logs", "ck_audit_logs_action"));

        await using var platform = CreatePlatformContext(database.ConnectionString);
        await using var organization = CreateOrganizationContext(database.ConnectionString);
        await using var audit = CreateAuditContext(database.ConnectionString);
        await using var identity = CreateIdentityContext(database.ConnectionString);
        Assert.Equal([FoundationMigration], await platform.Database.GetAppliedMigrationsAsync());
        Assert.Equal([OrganizationMigration, OrganizationAuthorizationMigration], await organization.Database.GetAppliedMigrationsAsync());
        Assert.Equal([AuditMigration, AuditAuthenticationMigration, AuditAuthorizationMigration], await audit.Database.GetAppliedMigrationsAsync());
        Assert.Equal([IdentityMigration, IdentityAuthorizationMigration], await identity.Database.GetAppliedMigrationsAsync());
        Assert.Equal(58, await identity.Permissions.CountAsync());
        Assert.Equal(
            PermissionCatalogue.AllCodes,
            await identity.Permissions.OrderBy(permission => permission.Code).Select(permission => permission.Code).ToArrayAsync());
        Assert.Empty(await identity.Roles.ToListAsync());
        Assert.Empty(await organization.UserBranchAccesses.ToListAsync());
    }

    [Fact]
    public async Task MilestoneZeroDatabase_UpgradesWithoutChangingPlatformData()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await using (var platform = CreatePlatformContext(database.ConnectionString))
        {
            await platform.Database.MigrateAsync(FoundationMigration);
            platform.SchemaMarkers.Add(new SchemaMarker(Guid.NewGuid(), "slice-one-upgrade", DateTimeOffset.UtcNow));
            await platform.SaveChangesAsync();
        }

        await ApplyAllMigrationsAsync(database.ConnectionString);
        await using var check = CreatePlatformContext(database.ConnectionString);
        Assert.Equal("slice-one-upgrade", await check.SchemaMarkers.Select(marker => marker.Name).SingleAsync());
    }

    [Fact]
    public async Task ExactSliceOneDatabase_UpgradesWithoutChangingExistingHistoriesOrSentinelData()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        Guid organizationId = Guid.NewGuid();
        Guid auditId = Guid.NewGuid();
        await using (var platform = CreatePlatformContext(database.ConnectionString))
        {
            await platform.Database.MigrateAsync(FoundationMigration);
            platform.SchemaMarkers.Add(new SchemaMarker(Guid.NewGuid(), "slice-two-platform-sentinel", new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero)));
            await platform.SaveChangesAsync();
        }

        await using (var organization = CreateOrganizationContext(database.ConnectionString))
        {
            await organization.Database.MigrateAsync(OrganizationMigration);
            organization.Organizations.Add(CreateOrganization(organizationId, "Slice One Sentinel", new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero)));
            await organization.SaveChangesAsync();
        }

        await using (var audit = CreateAuditContext(database.ConnectionString))
        {
            await audit.Database.MigrateAsync(AuditMigration);
            audit.AuditEntries.Add(CreateAuditEntry(auditId));
            await audit.SaveChangesAsync();
        }

        await ApplyAllMigrationsAsync(database.ConnectionString);

        await using var platformCheck = CreatePlatformContext(database.ConnectionString);
        await using var organizationCheck = CreateOrganizationContext(database.ConnectionString);
        await using var auditCheck = CreateAuditContext(database.ConnectionString);
        await using var identityCheck = CreateIdentityContext(database.ConnectionString);
        Assert.Equal("slice-two-platform-sentinel", await platformCheck.SchemaMarkers.Select(marker => marker.Name).SingleAsync());
        Assert.Equal(organizationId, await organizationCheck.Organizations.Select(organization => organization.Id).SingleAsync());
        Assert.Equal(auditId, await auditCheck.AuditEntries.Select(entry => entry.Id).SingleAsync());
        Assert.Equal([FoundationMigration], await platformCheck.Database.GetAppliedMigrationsAsync());
        Assert.Equal([OrganizationMigration, OrganizationAuthorizationMigration], await organizationCheck.Database.GetAppliedMigrationsAsync());
        Assert.Equal([AuditMigration, AuditAuthenticationMigration, AuditAuthorizationMigration], await auditCheck.Database.GetAppliedMigrationsAsync());
        Assert.Equal([IdentityMigration, IdentityAuthorizationMigration], await identityCheck.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task ExactSliceTwoDatabase_UpgradesWithoutFabricatingAuthorizationOrChangingUserData()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        Guid organizationId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 7, 21, 1, 0, 0, TimeSpan.Zero);

        await using (var platform = CreatePlatformContext(database.ConnectionString))
        {
            await platform.Database.MigrateAsync(FoundationMigration);
        }

        await using (var organization = CreateOrganizationContext(database.ConnectionString))
        {
            await organization.Database.MigrateAsync(OrganizationMigration);
            organization.Organizations.Add(CreateOrganization(organizationId, "Slice Two Organization", now));
            organization.Branches.Add(Branch.Create(
                branchId, organizationId, new OrganizationName("Cairo", 120), new BranchCode("CAI-01"),
                new LocaleCode("en"), new TimeZoneId("Africa/Cairo"), BusinessDayRollover.Parse("04:00"), now));
            await organization.SaveChangesAsync();
        }

        await using (var audit = CreateAuditContext(database.ConnectionString))
        {
            await audit.Database.MigrateAsync(AuditAuthenticationMigration);
        }

        await using (var identity = CreateIdentityContext(database.ConnectionString))
        {
            await identity.Database.MigrateAsync(IdentityMigration);
        }

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                insert into identity.users
                    (id, organization_id, username, normalized_username, email, normalized_email, display_name,
                     preferred_language, status, version, created_at_utc, updated_at_utc, activated_at_utc, archived_at_utc)
                values
                    (@user_id, @organization_id, 'manager', 'MANAGER', null, null, 'Slice Two User',
                     'en', 'Active', 2, @now, @now, @now, null);
                insert into identity.password_credentials
                    (id, user_id, encoded_hash, status, failed_attempt_count, failure_window_started_at_utc,
                     locked_until_utc, version, created_at_utc, updated_at_utc, password_changed_at_utc)
                values
                    (@credential_id, @user_id, '$kalm$existing-sentinel', 'Active', 0, null, null, 2, @now, @now, @now);
                """;
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("organization_id", organizationId);
            command.Parameters.AddWithValue("credential_id", Guid.NewGuid());
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync();
        }

        await ApplyAllMigrationsAsync(database.ConnectionString);

        await using var identityCheck = CreateIdentityContext(database.ConnectionString);
        User stored = await identityCheck.Users.SingleAsync();
        Assert.Equal(userId, stored.Id);
        Assert.Equal("Slice Two User", stored.DisplayName);
        Assert.Equal(1, stored.AuthorizationVersion);
        Assert.Equal(58, await identityCheck.Permissions.CountAsync());
        Assert.Empty(await identityCheck.Roles.ToListAsync());
        Assert.Empty(await identityCheck.RolePermissions.ToListAsync());
        Assert.Empty(await identityCheck.UserRoleAssignments.ToListAsync());
        await using var organizationCheck = CreateOrganizationContext(database.ConnectionString);
        Assert.Empty(await organizationCheck.UserBranchAccesses.ToListAsync());
        Assert.Empty(await organizationCheck.UserBranchAssignments.ToListAsync());
    }

    [Fact]
    public void PreviouslyReleasedMigrationFiles_RetainApprovedByteHashes()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/Kalm.Api/Migrations/20260715140000_InitialFoundation.cs"] = "4156f3e8350654755817217afb9311050d41b40f13ab049e8832bcb3d08b4f7e",
            ["src/Kalm.Api/Migrations/20260715140000_InitialFoundation.Designer.cs"] = "f70002a951453fbe225b3d5a9c41140c2fad0434a6b024cd38bb996a991862d9",
            ["src/Modules/Kalm.Organization.Infrastructure/Migrations/20260720181706_AddOrganizationFoundation.cs"] = "0fe74db01a13b2414c56834069ef2bc13db6b6ec592bec865b90462d2a131aef",
            ["src/Modules/Kalm.Organization.Infrastructure/Migrations/20260720181706_AddOrganizationFoundation.Designer.cs"] = "edb8d55ed9161e705d53b85a5eaff3b9aaafa9ecc15f59261a4b17d96a74890b",
            ["src/Modules/Kalm.Identity.Infrastructure/Migrations/20260720202353_AddManagementAuthentication.cs"] = "1718cb24f0a5ca6c5d9f6d135bcaca67b29a0b1c32996bd4d43b02b04ebba7ae",
            ["src/Modules/Kalm.Identity.Infrastructure/Migrations/20260720202353_AddManagementAuthentication.Designer.cs"] = "a4c899a6ccf453b118bce6032a821bd2662989dc117a57bb299c29768c96e2fb",
            ["src/Modules/Kalm.Audit.Infrastructure/Migrations/20260720181822_AddAuditFoundation.cs"] = "fd9c13d3fe0df9c2ab5536eb6bea69e56b4fb255afd996bf9ecb6e659a5cbb4e",
            ["src/Modules/Kalm.Audit.Infrastructure/Migrations/20260720181822_AddAuditFoundation.Designer.cs"] = "310c8f94770183214d62c84f5799ae741b7ec958555cd24e5ea9e98232c5d9d0",
            ["src/Modules/Kalm.Audit.Infrastructure/Migrations/20260720202409_ExtendManagementAuthenticationAuditActions.cs"] = "9baae94b3c5316d2992495ae884359550c8638109fad522239665827ee603afd",
            ["src/Modules/Kalm.Audit.Infrastructure/Migrations/20260720202409_ExtendManagementAuthenticationAuditActions.Designer.cs"] = "4116a164ae6e8f61b932c03a852cf0dcf2d5c421de4d0273ce705a35adde7e4f"
        };
        string root = FindRepositoryRoot();
        foreach ((string relativePath, string expectedHash) in expected)
        {
            using FileStream stream = File.OpenRead(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
        }
    }

    [Fact]
    public async Task CurrentModels_HaveNoPendingChangesForAnyDbContext()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await ApplyAllMigrationsAsync(database.ConnectionString);

        await using var platform = CreatePlatformContext(database.ConnectionString);
        await using var organization = CreateOrganizationContext(database.ConnectionString);
        await using var audit = CreateAuditContext(database.ConnectionString);
        await using var identity = CreateIdentityContext(database.ConnectionString);
        Assert.False(platform.Database.HasPendingModelChanges());
        Assert.False(organization.Database.HasPendingModelChanges());
        Assert.False(audit.Database.HasPendingModelChanges());
        Assert.False(identity.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task ConcurrentOrganizationCreation_CannotCreateTwoRows()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await ApplyAllMigrationsAsync(database.ConnectionString);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Task first = InsertOrganizationAsync(database.ConnectionString, "Kalm One", now);
        Task second = InsertOrganizationAsync(database.ConnectionString, "Kalm Two", now);

        Task[] results = [first, second];
        await Task.WhenAll(results.Select(task => IgnoreFailureAsync(task)));

        await using var context = CreateOrganizationContext(database.ConnectionString);
        Assert.Equal(1, await context.Organizations.CountAsync());
        Assert.Equal(1, results.Count(task => task.IsFaulted));
    }

    [Fact]
    public async Task BranchCode_IsUniqueWithinOrganization_AndRolloverRoundTrips()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await ApplyAllMigrationsAsync(database.ConnectionString);
        Guid organizationId = await InsertOrganizationAsync(database.ConnectionString, "Kalm", DateTimeOffset.UtcNow);
        await using var context = CreateOrganizationContext(database.ConnectionString);
        context.Branches.Add(CreateBranch(organizationId, "CAI-01"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var stored = await context.Branches.SingleAsync();
        Assert.Equal(new TimeOnly(4, 0), stored.BusinessDayRollover);
        context.Branches.Add(CreateBranch(organizationId, "cai-01"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task BranchAccessDeferredConstraint_EnforcesCompletedScopeAndCompositeOrganizationKeys()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await ApplyAllMigrationsAsync(database.ConnectionString);
        Guid organizationId = await InsertOrganizationAsync(database.ConnectionString, "Kalm", DateTimeOffset.UtcNow);
        Guid branchId;
        await using (var seed = CreateOrganizationContext(database.ConnectionString))
        {
            Branch branch = CreateBranch(organizationId, "CAI-01");
            branchId = branch.Id;
            seed.Branches.Add(branch);
            await seed.SaveChangesAsync();
        }

        await using (var missingAssignment = CreateOrganizationContext(database.ConnectionString))
        {
            missingAssignment.UserBranchAccesses.Add(UserBranchAccess.Create(
                Guid.NewGuid(), organizationId, Guid.NewGuid(), BranchAccessScope.AssignedBranches, DateTimeOffset.UtcNow));
            PostgresException failure = await AssertPostgresFailureAsync(() => missingAssignment.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal("ck_user_branch_access_completed_scope", failure.ConstraintName);
        }

        await using (var allWithAssignment = CreateOrganizationContext(database.ConnectionString))
        {
            var access = UserBranchAccess.Create(
                Guid.NewGuid(), organizationId, Guid.NewGuid(), BranchAccessScope.AllOrganizationBranches, DateTimeOffset.UtcNow);
            allWithAssignment.UserBranchAccesses.Add(access);
            allWithAssignment.UserBranchAssignments.Add(UserBranchAssignment.Assign(
                Guid.NewGuid(), access.Id, organizationId, branchId, DateTimeOffset.UtcNow));
            PostgresException failure = await AssertPostgresFailureAsync(() => allWithAssignment.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal("ck_user_branch_access_completed_scope", failure.ConstraintName);
        }

        await using (var crossOrganization = CreateOrganizationContext(database.ConnectionString))
        {
            Guid invalidOrganizationId = Guid.NewGuid();
            var access = UserBranchAccess.Create(
                Guid.NewGuid(), organizationId, Guid.NewGuid(), BranchAccessScope.AssignedBranches, DateTimeOffset.UtcNow);
            crossOrganization.UserBranchAccesses.Add(access);
            crossOrganization.UserBranchAssignments.Add(UserBranchAssignment.Assign(
                Guid.NewGuid(), access.Id, invalidOrganizationId, branchId, DateTimeOffset.UtcNow));
            PostgresException failure = await AssertPostgresFailureAsync(() => crossOrganization.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        }

        Guid validAccessId;
        Guid validAssignmentId;
        await using (var valid = CreateOrganizationContext(database.ConnectionString))
        {
            var access = UserBranchAccess.Create(
                Guid.NewGuid(), organizationId, Guid.NewGuid(), BranchAccessScope.AssignedBranches, DateTimeOffset.UtcNow);
            var assignment = UserBranchAssignment.Assign(
                Guid.NewGuid(), access.Id, organizationId, branchId, DateTimeOffset.UtcNow);
            validAccessId = access.Id;
            validAssignmentId = assignment.Id;
            valid.UserBranchAccesses.Add(access);
            valid.UserBranchAssignments.Add(assignment);
            await valid.SaveChangesAsync();
            Assert.Equal(1, await valid.UserBranchAccesses.CountAsync());
        }

        await using (var transitionToAll = CreateOrganizationContext(database.ConnectionString))
        {
            UserBranchAccess access = await transitionToAll.UserBranchAccesses.SingleAsync(candidate => candidate.Id == validAccessId);
            UserBranchAssignment assignment = await transitionToAll.UserBranchAssignments.SingleAsync(candidate => candidate.Id == validAssignmentId);
            assignment.Revoke(DateTimeOffset.UtcNow);
            access.ChangeScope(BranchAccessScope.AllOrganizationBranches, DateTimeOffset.UtcNow);
            await transitionToAll.SaveChangesAsync();
        }

        await using (var transitionToAssigned = CreateOrganizationContext(database.ConnectionString))
        {
            UserBranchAccess access = await transitionToAssigned.UserBranchAccesses.SingleAsync(candidate => candidate.Id == validAccessId);
            access.ChangeScope(BranchAccessScope.AssignedBranches, DateTimeOffset.UtcNow);
            var assignment = UserBranchAssignment.Assign(
                Guid.NewGuid(), access.Id, organizationId, branchId, DateTimeOffset.UtcNow);
            validAssignmentId = assignment.Id;
            transitionToAssigned.UserBranchAssignments.Add(assignment);
            await transitionToAssigned.SaveChangesAsync();
        }

        Guid secondAccessId;
        await using (var secondAccess = CreateOrganizationContext(database.ConnectionString))
        {
            Branch secondBranch = CreateBranch(organizationId, "CAI-02");
            var access = UserBranchAccess.Create(
                Guid.NewGuid(), organizationId, Guid.NewGuid(), BranchAccessScope.AssignedBranches, DateTimeOffset.UtcNow);
            secondAccessId = access.Id;
            secondAccess.Branches.Add(secondBranch);
            secondAccess.UserBranchAccesses.Add(access);
            secondAccess.UserBranchAssignments.Add(UserBranchAssignment.Assign(
                Guid.NewGuid(), access.Id, organizationId, secondBranch.Id, DateTimeOffset.UtcNow));
            await secondAccess.SaveChangesAsync();
        }

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
            await using var move = new NpgsqlCommand(
                "update organization.user_branch_assignments set access_id = @new_access_id where id = @assignment_id",
                connection,
                transaction);
            move.Parameters.AddWithValue("new_access_id", secondAccessId);
            move.Parameters.AddWithValue("assignment_id", validAssignmentId);
            Assert.Equal(1, await move.ExecuteNonQueryAsync());
            PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal("ck_user_branch_access_completed_scope", failure.ConstraintName);
        }
    }

    [Fact]
    public async Task AuthorizationConstraints_AreOrganizationScopedAndRejectCrossOrganizationAssignments()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await ApplyAllMigrationsAsync(database.ConnectionString);
        DateTimeOffset now = new(2026, 7, 21, 0, 0, 0, TimeSpan.Zero);
        Guid firstOrganizationId = Guid.NewGuid();
        Guid secondOrganizationId = Guid.NewGuid();
        Role firstRole = Role.Create(
            Guid.NewGuid(), firstOrganizationId, new RoleName("Operators"), "system.authorization-test", now);
        Role secondOrganizationRole = Role.Create(
            Guid.NewGuid(), secondOrganizationId, new RoleName("Operators"), "system.authorization-test", now);
        User user = User.Create(
            Guid.NewGuid(), firstOrganizationId, new Username("constraint-user"), null,
            new DisplayName("Constraint User"), "en", now);

        await using (var seed = CreateIdentityContext(database.ConnectionString))
        {
            seed.Roles.AddRange(firstRole, secondOrganizationRole);
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
        }

        await using (var duplicateName = CreateIdentityContext(database.ConnectionString))
        {
            duplicateName.Roles.Add(Role.Create(
                Guid.NewGuid(), firstOrganizationId, new RoleName("operators"), "system.different", now));
            PostgresException failure = await AssertPostgresFailureAsync(() => duplicateName.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
            Assert.Equal("ux_roles_organization_id_normalized_name", failure.ConstraintName);
        }

        await using (var duplicateSystemKey = CreateIdentityContext(database.ConnectionString))
        {
            duplicateSystemKey.Roles.Add(Role.Create(
                Guid.NewGuid(), firstOrganizationId, new RoleName("Different name"), "system.authorization-test", now));
            PostgresException failure = await AssertPostgresFailureAsync(() => duplicateSystemKey.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
            Assert.Equal("ux_roles_organization_id_system_key", failure.ConstraintName);
        }

        await using (var validAssignment = CreateIdentityContext(database.ConnectionString))
        {
            validAssignment.UserRoleAssignments.Add(UserRoleAssignment.Assign(
                Guid.NewGuid(), firstOrganizationId, user.Id, firstRole.Id, now));
            await validAssignment.SaveChangesAsync();
        }

        await using (var crossOrganization = CreateIdentityContext(database.ConnectionString))
        {
            crossOrganization.UserRoleAssignments.Add(UserRoleAssignment.Assign(
                Guid.NewGuid(), firstOrganizationId, user.Id, secondOrganizationRole.Id, now));
            PostgresException failure = await AssertPostgresFailureAsync(() => crossOrganization.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        }
    }

    [Fact]
    public async Task ExplicitCoordinator_CommitsOrganizationAndAuditTogether_AndRollsBackOnEitherFailure()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await ApplyAllMigrationsAsync(database.ConnectionString);
        var coordinator = new SliceOneOrganizationAuditTransactionCoordinator(Options.Create(new DatabaseOptions { ConnectionString = database.ConnectionString }));
        Guid organizationId = Guid.NewGuid();

        await coordinator.ExecuteOrganizationAuditAsync(async (organization, auditWriter, _, cancellationToken) =>
        {
            organization.Organizations.Add(CreateOrganization(organizationId, "Kalm"));
            await auditWriter.AppendAsync(CreateAuditRequest(organizationId), cancellationToken);
        }, CancellationToken.None);

        await using (var verify = CreateOrganizationContext(database.ConnectionString))
        {
            Assert.Equal(1, await verify.Organizations.CountAsync());
        }

        await using (var verify = CreateAuditContext(database.ConnectionString))
        {
            Assert.Equal(1, await verify.AuditEntries.CountAsync());
        }

        await Assert.ThrowsAsync<ArgumentException>(() => coordinator.ExecuteOrganizationAuditAsync(async (organization, auditWriter, _, cancellationToken) =>
        {
            organization.Organizations.Add(CreateOrganization(Guid.NewGuid(), "Rollback"));
            await auditWriter.AppendAsync(CreateAuditRequest(Guid.NewGuid()) with { EntityType = new string('x', 101) }, cancellationToken);
        }, CancellationToken.None));

        await using var afterRollback = CreateOrganizationContext(database.ConnectionString);
        Assert.Equal(1, await afterRollback.Organizations.CountAsync());

        await Assert.ThrowsAsync<DbUpdateException>(() => coordinator.ExecuteOrganizationAuditAsync(async (organization, auditWriter, _, cancellationToken) =>
        {
            organization.Organizations.Add(CreateOrganization(Guid.NewGuid(), "Organization failure"));
            await auditWriter.AppendAsync(CreateAuditRequest(Guid.NewGuid()), cancellationToken);
        }, CancellationToken.None));

        await using var auditAfterOrganizationFailure = CreateAuditContext(database.ConnectionString);
        Assert.Equal(1, await auditAfterOrganizationFailure.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task StaleOrganizationUpdate_IsRejectedAndItsAuditEntryRollsBack()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await ApplyAllMigrationsAsync(database.ConnectionString);
        Guid organizationId = await InsertOrganizationAsync(database.ConnectionString, "Kalm", DateTimeOffset.UtcNow);

        await using var first = CreateOrganizationContext(database.ConnectionString);
        await using var staleSource = CreateOrganizationContext(database.ConnectionString);
        var current = await first.Organizations.SingleAsync();
        var stale = await staleSource.Organizations.SingleAsync();
        current.Update(new OrganizationName("Kalm Current", 120), null, new CurrencyCode("EGP"), new LocaleCode("en"), DateTimeOffset.UtcNow);
        await first.SaveChangesAsync();
        stale.Update(new OrganizationName("Kalm Stale", 120), null, new CurrencyCode("EGP"), new LocaleCode("en"), DateTimeOffset.UtcNow);

        var coordinator = new SliceOneOrganizationAuditTransactionCoordinator(Options.Create(new DatabaseOptions { ConnectionString = database.ConnectionString }));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => coordinator.ExecuteOrganizationAuditAsync(async (organization, auditWriter, _, cancellationToken) =>
        {
            organization.Attach(stale);
            organization.Entry(stale).State = EntityState.Modified;
            organization.Entry(stale).Property(nameof(Kalm.Organization.Domain.Organization.Version)).OriginalValue = 1L;
            await auditWriter.AppendAsync(CreateAuditRequest(organizationId), cancellationToken);
        }, CancellationToken.None));

        await using var verifyOrganization = CreateOrganizationContext(database.ConnectionString);
        Assert.Equal("Kalm Current", (await verifyOrganization.Organizations.SingleAsync()).BrandName);
        await using var verifyAudit = CreateAuditContext(database.ConnectionString);
        Assert.Empty(await verifyAudit.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task AuditLog_RejectsEfAndDirectSqlUpdateDeleteWhileAllowingInsert()
    {
        await using var database = await SliceOneDatabase.CreateAsync();
        await ApplyAllMigrationsAsync(database.ConnectionString);
        Guid auditId = Guid.NewGuid();
        await using (var context = CreateAuditContext(database.ConnectionString))
        {
            context.AuditEntries.Add(CreateAuditEntry(auditId));
            await context.SaveChangesAsync();
            Assert.Equal(1, await context.AuditEntries.CountAsync());
            var entry = await context.AuditEntries.SingleAsync();
            context.Entry(entry).Property(nameof(AuditEntry.EntityType)).CurrentValue = "Changed";
            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await context.SaveChangesAsync();
            });
            context.ChangeTracker.Clear();
            context.AuditEntries.Remove(await context.AuditEntries.SingleAsync());
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var update = new NpgsqlCommand("update audit.audit_logs set entity_type = 'Changed' where id = @id", connection);
        update.Parameters.AddWithValue("id", auditId);
        await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        await using var delete = new NpgsqlCommand("delete from audit.audit_logs where id = @id", connection);
        delete.Parameters.AddWithValue("id", auditId);
        await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
    }

    private static async Task ApplyAllMigrationsAsync(string connectionString)
    {
        await using var platform = CreatePlatformContext(connectionString);
        await platform.Database.MigrateAsync();
        await using var organization = CreateOrganizationContext(connectionString);
        await organization.Database.MigrateAsync();
        await using var audit = CreateAuditContext(connectionString);
        await audit.Database.MigrateAsync();
        await using var identity = CreateIdentityContext(connectionString);
        await identity.Database.MigrateAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kalm.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static KalmDbContext CreatePlatformContext(string connectionString) => new(new DbContextOptionsBuilder<KalmDbContext>().UseNpgsql(connectionString).Options);
    private static OrganizationDbContext CreateOrganizationContext(string connectionString) => new(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "organization")).Options);
    private static AuditDbContext CreateAuditContext(string connectionString) => new(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "audit")).Options);
    private static IdentityDbContext CreateIdentityContext(string connectionString) => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "identity")).Options);

    private static async Task<Guid> InsertOrganizationAsync(string connectionString, string name, DateTimeOffset now)
    {
        Guid id = Guid.NewGuid();
        await using var context = CreateOrganizationContext(connectionString);
        context.Organizations.Add(CreateOrganization(id, name, now));
        await context.SaveChangesAsync();
        return id;
    }

    private static async Task IgnoreFailureAsync(Task task)
    {
        try { await task; } catch (DbUpdateException) { }
    }

    private static async Task<PostgresException> AssertPostgresFailureAsync(Func<Task> action)
    {
        Exception failure = await Assert.ThrowsAnyAsync<Exception>(action);
        PostgresException? postgres = failure as PostgresException ?? failure.InnerException as PostgresException;
        return Assert.IsType<PostgresException>(postgres);
    }

    private static Kalm.Organization.Domain.Organization CreateOrganization(Guid id, string name, DateTimeOffset? now = null) => Kalm.Organization.Domain.Organization.Create(id, new OrganizationName(name, 120), null, new CurrencyCode("EGP"), new LocaleCode("en"), now ?? DateTimeOffset.UtcNow);
    private static Branch CreateBranch(Guid organizationId, string code) => Branch.Create(Guid.NewGuid(), organizationId, new OrganizationName("Cairo", 120), new BranchCode(code), new LocaleCode("ar-EG"), new TimeZoneId("Africa/Cairo"), BusinessDayRollover.Parse("04:00"), DateTimeOffset.UtcNow);
    private static AuditWriteRequest CreateAuditRequest(Guid organizationId) => new(Guid.NewGuid(), DateTimeOffset.UtcNow, organizationId, null, null, null, AuditActorType.System, null, AuditAction.OrganizationCreated, "Organization", organizationId, AuditResult.Succeeded, null, "slice-one-test", null, "{\"brandName\":\"Kalm\"}", null, null);
    private static AuditEntry CreateAuditEntry(Guid id) => AuditEntry.Create(id, DateTimeOffset.UtcNow, null, null, null, null, AuditActorType.System, null, AuditAction.OrganizationCreated, "Organization", null, AuditResult.Succeeded, null, "slice-one-test", null, null, null, null);

    private static async Task<string?> ResolveRegClassAsync(string connectionString, string name)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select to_regclass(@name)::text", connection);
        command.Parameters.AddWithValue("name", name);
        object? result = await command.ExecuteScalarAsync();
        return result is DBNull ? null : (string?)result;
    }

    private static async Task<string?> ResolveTriggerAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select tgname from pg_trigger where tgrelid = 'audit.audit_logs'::regclass and not tgisinternal", connection);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task<bool> ConstraintExistsAsync(string connectionString, string schema, string table, string constraint)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select exists (select 1 from pg_constraint where connamespace = @schema::regnamespace and conrelid = (@schema || '.' || @table)::regclass and conname = @constraint)", connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("constraint", constraint);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private sealed class SliceOneDatabase : IAsyncDisposable
    {
        private const string DefaultAdmin = "Host=localhost;Port=54329;Database=postgres;Username=kalm;Password=kalm_dev_password";
        private readonly string _admin;
        private readonly string _name;
        private SliceOneDatabase(string admin, string connectionString, string name) { _admin = admin; ConnectionString = connectionString; _name = name; }
        public string ConnectionString { get; }
        public static async Task<SliceOneDatabase> CreateAsync()
        {
            string admin = Environment.GetEnvironmentVariable("KALM_TEST_POSTGRES_ADMIN") ?? DefaultAdmin;
            string name = $"kalm_sliceone_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"create database \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
            var builder = new NpgsqlConnectionStringBuilder(admin) { Database = name };
            return new SliceOneDatabase(admin, builder.ConnectionString, name);
        }
        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_admin) { Pooling = false }.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"drop database if exists \"{_name}\" with (force)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
