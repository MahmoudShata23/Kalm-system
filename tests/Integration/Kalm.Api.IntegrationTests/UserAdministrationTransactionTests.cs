using Kalm.Api.Configuration;
using Kalm.Api.Transactions;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity;
using Kalm.Identity.Authorization;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Identity.Infrastructure.Security;
using Kalm.Organization.Domain;
using Kalm.Organization.Domain.ValueObjects;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OrganizationAggregate = Kalm.Organization.Domain.Organization;

namespace Kalm.Api.IntegrationTests;

public sealed class UserAdministrationTransactionTests
{
    private const string Password = "Slice5.Password!";
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateUpdateAndNoOp_AreAtomicAndAdvanceAuthorizationExactlyOnce()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid firstBranchId, Guid secondBranchId) = await SeedOrganizationAsync(database.ConnectionString);
        Guid firstRoleId = await SeedRoleAsync(database.ConnectionString, organizationId, "Cashier", PermissionCodes.PosSell);
        Guid secondRoleId = await SeedRoleAsync(database.ConnectionString, organizationId, "Reports", PermissionCodes.ReportsSales);
        var coordinator = CreateCoordinator(database.ConnectionString);

        UserOperationResult created = await coordinator.CreateAsync(
            organizationId,
            Guid.NewGuid(),
            new UserCreateRequest
            {
                Username = "employee.one",
                Email = "employee.one@kalm.local",
                DisplayName = "Employee One",
                PreferredLanguage = "en",
                RoleIds = [firstRoleId],
                BranchAccessScope = "assignedBranches",
                BranchIds = [firstBranchId]
            },
            "create-user",
            CancellationToken.None);
        Assert.True(created.Succeeded);
        Assert.Equal("suspended", created.Detail!.User.Status);
        Assert.Equal("pendingSetup", created.Detail.User.CredentialStatus);
        Assert.Equal(1, created.Detail.Version);

        Guid userId = created.Detail.User.Id;
        UserOperationResult updated = await coordinator.UpdateAsync(
            organizationId,
            Guid.NewGuid(),
            userId,
            1,
            new UserUpdateRequest(
                "employee.one", "employee.one@kalm.local", "Employee One Updated", "ar",
                [secondRoleId], "assignedBranches", [secondBranchId]),
            "update-user",
            CancellationToken.None);
        Assert.True(updated.Succeeded);
        Assert.Equal(2, updated.Detail!.Version);

        await using (var identity = CreateIdentity(database.ConnectionString))
        {
            User user = await identity.Users.SingleAsync(candidate => candidate.Id == userId);
            Assert.Equal(2, user.AuthorizationVersion);
            Assert.Equal(2, user.Version);
            UserRoleAssignment[] history = await identity.UserRoleAssignments.Where(assignment => assignment.UserId == userId).ToArrayAsync();
            Assert.Equal(2, history.Length);
            Assert.Single(history, assignment => assignment.RoleId == firstRoleId && assignment.RevokedAtUtc != null);
            Assert.Single(history, assignment => assignment.RoleId == secondRoleId && assignment.RevokedAtUtc == null);
        }

        await using (var organization = CreateOrganization(database.ConnectionString))
        {
            UserBranchAccess access = await organization.UserBranchAccesses.SingleAsync(candidate => candidate.UserId == userId);
            UserBranchAssignment[] history = await organization.UserBranchAssignments.Where(assignment => assignment.AccessId == access.Id).ToArrayAsync();
            Assert.Equal(2, history.Length);
            Assert.Single(history, assignment => assignment.BranchId == firstBranchId && assignment.RevokedAtUtc != null);
            Assert.Single(history, assignment => assignment.BranchId == secondBranchId && assignment.RevokedAtUtc == null);
        }

        await using var auditBefore = CreateAudit(database.ConnectionString);
        int countBefore = await auditBefore.AuditEntries.CountAsync();
        UserOperationResult noOp = await coordinator.UpdateAsync(
            organizationId,
            Guid.NewGuid(),
            userId,
            2,
            new UserUpdateRequest(
                "employee.one", "employee.one@kalm.local", "Employee One Updated", "ar",
                [secondRoleId], "assignedBranches", [secondBranchId]),
            "no-op-user",
            CancellationToken.None);
        Assert.True(noOp.Succeeded);
        Assert.Equal(2, noOp.Detail!.Version);
        await using var auditAfter = CreateAudit(database.ConnectionString);
        Assert.Equal(countBefore, await auditAfter.AuditEntries.CountAsync());
        var auditJson = await auditAfter.AuditEntries
            .Where(entry => entry.EntityId == userId)
            .Select(entry => new { entry.BeforeJson, entry.AfterJson })
            .ToArrayAsync();
        string auditPayload = string.Concat(auditJson.Select(entry => (entry.BeforeJson ?? string.Empty) + (entry.AfterJson ?? string.Empty)));
        Assert.DoesNotContain("employee.one@kalm.local", auditPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, auditPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActivationPasswordResetAndSuspension_EnforceLifecycleAndRevokeSessions()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid branchId, _) = await SeedOrganizationAsync(database.ConnectionString);
        Guid roleId = await SeedRoleAsync(database.ConnectionString, organizationId, "Cashier", PermissionCodes.PosSell);
        var coordinator = CreateCoordinator(database.ConnectionString);
        UserOperationResult created = await coordinator.CreateAsync(
            organizationId,
            Guid.NewGuid(),
            CreateRequest("employee.two", roleId, branchId),
            "create",
            CancellationToken.None);
        Guid userId = created.Detail!.User.Id;

        UserOperationResult activationRejected = await coordinator.ActivateAsync(
            organizationId, Guid.NewGuid(), userId, 1, "activate-rejected", CancellationToken.None);
        Assert.False(activationRejected.Succeeded);
        Assert.Equal("user.activation_requirements_not_met", activationRejected.ErrorCode);

        UserOperationResult passwordSet = await coordinator.SetPasswordAsync(
            organizationId, Guid.NewGuid(), userId, 1, Password, "password-set", CancellationToken.None);
        Assert.True(passwordSet.Succeeded);
        Assert.Equal(2, passwordSet.Detail!.Version);
        UserOperationResult activated = await coordinator.ActivateAsync(
            organizationId, Guid.NewGuid(), userId, 2, "activate", CancellationToken.None);
        Assert.True(activated.Succeeded);
        Assert.Equal("active", activated.Detail!.User.Status);
        Assert.Equal(1, await AuthorizationVersionAsync(database.ConnectionString, userId));

        await using (var identity = CreateIdentity(database.ConnectionString))
        {
            identity.UserSessions.Add(UserSession.Create(Guid.NewGuid(), userId, Now, TimeSpan.FromMinutes(20), TimeSpan.FromHours(8)));
            await identity.SaveChangesAsync();
        }

        UserOperationResult reset = await coordinator.SetPasswordAsync(
            organizationId, Guid.NewGuid(), userId, 3, Password + "2", "password-reset", CancellationToken.None);
        Assert.True(reset.Succeeded);
        Assert.Equal(4, reset.Detail!.Version);
        await using (var identity = CreateIdentity(database.ConnectionString))
        {
            Assert.All(await identity.UserSessions.Where(session => session.UserId == userId).ToArrayAsync(), session =>
            {
                Assert.NotNull(session.RevokedAtUtc);
                Assert.Equal(SessionRevocationReason.CredentialChanged, session.RevocationReason);
            });
            identity.UserSessions.Add(UserSession.Create(Guid.NewGuid(), userId, Now, TimeSpan.FromMinutes(20), TimeSpan.FromHours(8)));
            await identity.SaveChangesAsync();
        }

        UserOperationResult suspended = await coordinator.SuspendAsync(
            organizationId, Guid.NewGuid(), userId, 4, false, "suspend", CancellationToken.None);
        Assert.True(suspended.Succeeded);
        Assert.Equal("suspended", suspended.Detail!.User.Status);
        Assert.Equal(2, await AuthorizationVersionAsync(database.ConnectionString, userId));
        await using var verify = CreateIdentity(database.ConnectionString);
        Assert.All(await verify.UserSessions.Where(session => session.UserId == userId).ToArrayAsync(), session => Assert.NotNull(session.RevokedAtUtc));
    }

    [Fact]
    public async Task SuspendingFinalManagementUser_RollsBackAndAuditsProtection()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid branchId, _) = await SeedOrganizationAsync(database.ConnectionString);
        Guid roleId = await SeedRoleAsync(database.ConnectionString, organizationId, "Management", PermissionCodes.ManagementAccess);
        var coordinator = CreateCoordinator(database.ConnectionString);
        UserCreateRequest request = CreateRequest("last.manager", roleId, branchId);
        request.InitialPassword = Password;
        UserOperationResult created = await coordinator.CreateAsync(
            organizationId, Guid.NewGuid(), request, "create-manager", CancellationToken.None);
        Guid userId = created.Detail!.User.Id;
        UserOperationResult activated = await coordinator.ActivateAsync(
            organizationId, userId, userId, 1, "activate-manager", CancellationToken.None);

        UserOperationResult rejected = await coordinator.SuspendAsync(
            organizationId, userId, userId, activated.Detail!.Version, true, "suspend-last-manager", CancellationToken.None);
        Assert.False(rejected.Succeeded);
        Assert.Equal("user.last_management_access", rejected.ErrorCode);
        await using var identity = CreateIdentity(database.ConnectionString);
        Assert.Equal(UserStatus.Active, (await identity.Users.SingleAsync(candidate => candidate.Id == userId)).Status);
        await using var audit = CreateAudit(database.ConnectionString);
        Assert.Single(await audit.AuditEntries.Where(entry => entry.CorrelationId == "suspend-last-manager"
            && entry.Action == AuditAction.LastManagementAccessProtectionTriggered).ToArrayAsync());
        Assert.Empty(await audit.AuditEntries.Where(entry => entry.CorrelationId == "suspend-last-manager"
            && entry.Action == AuditAction.UserSuspended).ToArrayAsync());
    }

    [Fact]
    public async Task AuditFailure_RollsBackIdentityAndOrganizationWrites()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid branchId, _) = await SeedOrganizationAsync(database.ConnectionString);
        Guid roleId = await SeedRoleAsync(database.ConnectionString, organizationId, "Cashier", PermissionCodes.PosSell);
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "alter table audit.audit_logs add constraint ck_test_reject_user_created check (action <> 'UserCreated')",
                connection);
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAnyAsync<Exception>(() => CreateCoordinator(database.ConnectionString).CreateAsync(
            organizationId,
            Guid.NewGuid(),
            CreateRequest("rollback.user", roleId, branchId),
            "rollback-create",
            CancellationToken.None));
        await using var identity = CreateIdentity(database.ConnectionString);
        Assert.Empty(await identity.Users.Where(user => user.Username == "rollback.user").ToArrayAsync());
        await using var organization = CreateOrganization(database.ConnectionString);
        Assert.Empty(await organization.UserBranchAccesses.ToArrayAsync());
    }

    [Fact]
    public async Task CrossOrganizationAndStaleVersions_AreIndistinguishableOrRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid branchId, _) = await SeedOrganizationAsync(database.ConnectionString);
        Guid roleId = await SeedRoleAsync(database.ConnectionString, organizationId, "Cashier", PermissionCodes.PosSell);
        var coordinator = CreateCoordinator(database.ConnectionString);
        UserOperationResult created = await coordinator.CreateAsync(
            organizationId, Guid.NewGuid(), CreateRequest("etag.user", roleId, branchId), "create", CancellationToken.None);
        Guid userId = created.Detail!.User.Id;

        UserOperationResult crossOrganization = await coordinator.UpdateAsync(
            Guid.NewGuid(), Guid.NewGuid(), userId, 1,
            new UserUpdateRequest("etag.user", null, "ETag User", "en", [roleId], "assignedBranches", [branchId]),
            "cross-org", CancellationToken.None);
        Assert.False(crossOrganization.Succeeded);
        Assert.Equal("user.not_found", crossOrganization.ErrorCode);

        UserOperationResult stale = await coordinator.UpdateAsync(
            organizationId, Guid.NewGuid(), userId, 99,
            new UserUpdateRequest("etag.user", null, "ETag User", "en", [roleId], "assignedBranches", [branchId]),
            "stale", CancellationToken.None);
        Assert.False(stale.Succeeded);
        Assert.Equal("user.concurrency_conflict", stale.ErrorCode);
        Assert.Equal(1, stale.CurrentVersion);
    }

    private static UserCreateRequest CreateRequest(string username, Guid roleId, Guid branchId) => new()
    {
        Username = username,
        DisplayName = username,
        PreferredLanguage = "en",
        RoleIds = [roleId],
        BranchAccessScope = "assignedBranches",
        BranchIds = [branchId]
    };

    private static UserAdministrationAuditTransactionCoordinator CreateCoordinator(string connectionString)
        => new(
            Options.Create(new DatabaseOptions { ConnectionString = connectionString }),
            new FixedClock(Now),
            new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions())));

    private static async Task MigrateAsync(string connectionString)
    {
        await using var organization = CreateOrganization(connectionString);
        await organization.Database.MigrateAsync();
        await using var identity = CreateIdentity(connectionString);
        await identity.Database.MigrateAsync();
        await using var audit = CreateAudit(connectionString);
        await audit.Database.MigrateAsync();
    }

    private static async Task<(Guid OrganizationId, Guid FirstBranchId, Guid SecondBranchId)> SeedOrganizationAsync(string connectionString)
    {
        Guid organizationId = Guid.NewGuid();
        Guid firstBranchId = Guid.NewGuid();
        Guid secondBranchId = Guid.NewGuid();
        await using var context = CreateOrganization(connectionString);
        OrganizationAggregate organization = OrganizationAggregate.Create(
            organizationId, new OrganizationName("Kalm", 120), null, new CurrencyCode("EGP"), new LocaleCode("en"), Now);
        organization.ChangeStatus(OrganizationStatus.Active, Now);
        Branch first = Branch.Create(
            firstBranchId, organizationId, new OrganizationName("First", 120), new BranchCode("FIRST"),
            new LocaleCode("en"), new TimeZoneId("Africa/Cairo"), new BusinessDayRollover(new TimeOnly(4, 0)), Now);
        first.ChangeStatus(BranchStatus.Active, Now);
        Branch second = Branch.Create(
            secondBranchId, organizationId, new OrganizationName("Second", 120), new BranchCode("SECOND"),
            new LocaleCode("en"), new TimeZoneId("Africa/Cairo"), new BusinessDayRollover(new TimeOnly(4, 0)), Now);
        second.ChangeStatus(BranchStatus.Active, Now);
        context.Organizations.Add(organization);
        context.Branches.AddRange(first, second);
        await context.SaveChangesAsync();
        return (organizationId, firstBranchId, secondBranchId);
    }

    private static async Task<Guid> SeedRoleAsync(string connectionString, Guid organizationId, string name, string permissionCode)
    {
        Guid roleId = Guid.NewGuid();
        await using var identity = CreateIdentity(connectionString);
        Role role = Role.Create(roleId, organizationId, new RoleName(name), null, Now);
        Permission permission = await identity.Permissions.SingleAsync(candidate => candidate.Code == permissionCode);
        identity.Roles.Add(role);
        identity.RolePermissions.Add(RolePermission.Grant(Guid.NewGuid(), roleId, permission.Id, Now));
        await identity.SaveChangesAsync();
        return roleId;
    }

    private static async Task<long> AuthorizationVersionAsync(string connectionString, Guid userId)
    {
        await using var identity = CreateIdentity(connectionString);
        return await identity.Users.Where(user => user.Id == userId).Select(user => user.AuthorizationVersion).SingleAsync();
    }

    private static IdentityDbContext CreateIdentity(string connectionString) => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "identity")).Options);
    private static OrganizationDbContext CreateOrganization(string connectionString) => new(new DbContextOptionsBuilder<OrganizationDbContext>()
        .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "organization")).Options);
    private static AuditDbContext CreateAudit(string connectionString) => new(new DbContextOptionsBuilder<AuditDbContext>()
        .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "audit")).Options);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private const string DefaultAdmin = "Host=localhost;Port=54329;Database=postgres;Username=kalm;Password=kalm_dev_password";
        private readonly string _admin;
        private readonly string _name;

        private TestDatabase(string admin, string connectionString, string name)
        {
            _admin = admin;
            ConnectionString = connectionString;
            _name = name;
        }

        public string ConnectionString { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            string admin = Environment.GetEnvironmentVariable("KALM_TEST_POSTGRES_ADMIN") ?? DefaultAdmin;
            string name = $"kalm_user_tx_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"create database \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
            return new TestDatabase(admin, new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString, name);
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
