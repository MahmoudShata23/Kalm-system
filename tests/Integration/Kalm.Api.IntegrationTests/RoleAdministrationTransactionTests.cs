using Kalm.Api.Configuration;
using Kalm.Api.Transactions;
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

namespace Kalm.Api.IntegrationTests;

public sealed class RoleAdministrationTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateUpdateNoOpAndArchive_UseExpectedVersionsAndAuditEvents()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        var coordinator = CreateCoordinator(database.ConnectionString);

        RoleOperationResult created = await coordinator.CreateAsync(
            organizationId,
            actorId,
            new RoleWriteRequest("Cafe Manager", [PermissionCodes.ReportsSales]),
            "create-role",
            CancellationToken.None);
        Assert.True(created.Succeeded);
        Assert.Equal(1, created.Detail!.Version);
        Guid roleId = created.Detail.Role.Id;

        RoleOperationResult updated = await coordinator.UpdateAsync(
            organizationId,
            actorId,
            roleId,
            1,
            new RoleWriteRequest("Operations Manager", [PermissionCodes.ReportsInventory, PermissionCodes.ReportsSales]),
            "update-role",
            CancellationToken.None);
        Assert.True(updated.Succeeded);
        Assert.Equal(2, updated.Detail!.Version);
        Assert.Equal([PermissionCodes.ReportsInventory, PermissionCodes.ReportsSales], updated.Detail.Role.PermissionCodes);

        await using var auditBeforeNoOp = CreateAudit(database.ConnectionString);
        int auditCount = await auditBeforeNoOp.AuditEntries.CountAsync();
        RoleOperationResult noOp = await coordinator.UpdateAsync(
            organizationId,
            actorId,
            roleId,
            2,
            new RoleWriteRequest("Operations Manager", [PermissionCodes.ReportsSales, PermissionCodes.ReportsInventory]),
            "no-op",
            CancellationToken.None);
        Assert.True(noOp.Succeeded);
        Assert.Equal(2, noOp.Detail!.Version);
        await using var auditAfterNoOp = CreateAudit(database.ConnectionString);
        Assert.Equal(auditCount, await auditAfterNoOp.AuditEntries.CountAsync());

        RoleOperationResult archived = await coordinator.ArchiveAsync(
            organizationId, actorId, roleId, 2, "archive-role", CancellationToken.None);
        Assert.True(archived.Succeeded);
        Assert.True(archived.WasArchived);

        await using var verify = CreateIdentity(database.ConnectionString);
        Role role = await verify.Roles.SingleAsync(candidate => candidate.Id == roleId);
        Assert.Equal(RoleStatus.Archived, role.Status);
        Assert.Equal(3, role.Version);
        await using var audit = CreateAudit(database.ConnectionString);
        AuditAction[] actions = await audit.AuditEntries.OrderBy(entry => entry.OccurredAtUtc).Select(entry => entry.Action).ToArrayAsync();
        Assert.Contains(AuditAction.RoleCreated, actions);
        Assert.Contains(AuditAction.RoleRenamed, actions);
        Assert.Contains(AuditAction.RolePermissionSetChanged, actions);
        Assert.Contains(AuditAction.RoleArchived, actions);
    }

    [Fact]
    public async Task PermissionDiff_IncrementsEveryAssignedUserExactlyOnceAndAssignedRoleCannotArchive()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        var coordinator = CreateCoordinator(database.ConnectionString);
        RoleOperationResult created = await coordinator.CreateAsync(
            organizationId, Guid.NewGuid(), new RoleWriteRequest("Reports", [PermissionCodes.ReportsSales]), "create", CancellationToken.None);
        Guid roleId = created.Detail!.Role.Id;
        Guid activeUserId = await AddUserAsync(database.ConnectionString, organizationId, roleId, "active-manager", activate: true);
        Guid suspendedUserId = await AddUserAsync(database.ConnectionString, organizationId, roleId, "suspended-manager", activate: false);

        RoleOperationResult updated = await coordinator.UpdateAsync(
            organizationId,
            activeUserId,
            roleId,
            1,
            new RoleWriteRequest("Reports", [PermissionCodes.ReportsCost]),
            "change-permissions",
            CancellationToken.None);
        Assert.True(updated.Succeeded);

        await using var identity = CreateIdentity(database.ConnectionString);
        User[] users = await identity.Users.Where(user => user.Id == activeUserId || user.Id == suspendedUserId).OrderBy(user => user.Id).ToArrayAsync();
        Assert.All(users, user => Assert.Equal(2, user.AuthorizationVersion));
        Assert.Equal(3, users.Single(user => user.Id == activeUserId).Version);
        Assert.Equal(2, users.Single(user => user.Id == suspendedUserId).Version);

        RoleOperationResult archive = await coordinator.ArchiveAsync(
            organizationId, activeUserId, roleId, 2, "archive-assigned", CancellationToken.None);
        Assert.False(archive.Succeeded);
        Assert.Equal("role.has_active_assignments", archive.ErrorCode);
        Assert.Equal(2, archive.ActiveAssignmentCount);
    }

    [Fact]
    public async Task RemovingFinalManagementAccess_RollsBackMutationAndWritesOnlyRejectionAudit()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        var coordinator = CreateCoordinator(database.ConnectionString);
        RoleOperationResult created = await coordinator.CreateAsync(
            organizationId,
            Guid.NewGuid(),
            new RoleWriteRequest("Management", [PermissionCodes.ManagementAccess, PermissionCodes.RolesManage]),
            "create-management",
            CancellationToken.None);
        Guid roleId = created.Detail!.Role.Id;
        Guid userId = await AddUserAsync(database.ConnectionString, organizationId, roleId, "last-manager", activate: true);

        RoleOperationResult rejected = await coordinator.UpdateAsync(
            organizationId,
            userId,
            roleId,
            1,
            new RoleWriteRequest("Management", [PermissionCodes.RolesManage]),
            "remove-last-management",
            CancellationToken.None);
        Assert.False(rejected.Succeeded);
        Assert.Equal("role.last_management_access", rejected.ErrorCode);

        await using var identity = CreateIdentity(database.ConnectionString);
        Role role = await identity.Roles.SingleAsync(candidate => candidate.Id == roleId);
        Assert.Equal(1, role.Version);
        User user = await identity.Users.SingleAsync(candidate => candidate.Id == userId);
        Assert.Equal(1, user.AuthorizationVersion);
        string[] codes = await (
            from grant in identity.RolePermissions
            join permission in identity.Permissions on grant.PermissionId equals permission.Id
            where grant.RoleId == roleId && grant.RevokedAtUtc == null
            orderby permission.Code
            select permission.Code).ToArrayAsync();
        Assert.Equal([PermissionCodes.ManagementAccess, PermissionCodes.RolesManage], codes);

        await using var audit = CreateAudit(database.ConnectionString);
        Assert.Single(await audit.AuditEntries.Where(entry => entry.Action == AuditAction.LastManagementAccessProtectionTriggered).ToArrayAsync());
        Assert.Empty(await audit.AuditEntries.Where(entry => entry.CorrelationId == "remove-last-management" && entry.Action == AuditAction.RolePermissionSetChanged).ToArrayAsync());
    }

    [Fact]
    public async Task ProtectedSystemRole_IsRejectedBeforeAnyNormalMutation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        Guid roleId = Guid.NewGuid();
        await using (var identity = CreateIdentity(database.ConnectionString))
        {
            Role role = Role.Create(
                roleId, organizationId, new RoleName("Initial Administrator"),
                PermissionCatalogue.FirstAdministratorSystemRoleKey, Now);
            identity.Roles.Add(role);
            Permission permission = await identity.Permissions.SingleAsync(candidate => candidate.Code == PermissionCodes.ManagementAccess);
            identity.RolePermissions.Add(RolePermission.Grant(Guid.NewGuid(), roleId, permission.Id, Now));
            await identity.SaveChangesAsync();
        }

        RoleOperationResult result = await CreateCoordinator(database.ConnectionString).UpdateAsync(
            organizationId,
            Guid.NewGuid(),
            roleId,
            1,
            new RoleWriteRequest("Renamed", [PermissionCodes.ManagementAccess]),
            "system-rejected",
            CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal("role.system_role_protected", result.ErrorCode);

        await using var verify = CreateIdentity(database.ConnectionString);
        Assert.Equal("Initial Administrator", (await verify.Roles.SingleAsync()).Name);
    }

    private static async Task<Guid> AddUserAsync(string connectionString, Guid organizationId, Guid roleId, string username, bool activate)
    {
        Guid userId = Guid.NewGuid();
        await using var identity = CreateIdentity(connectionString);
        User user = User.Create(userId, organizationId, new Username(username), null, new DisplayName(username), "en", Now);
        if (activate)
        {
            PasswordCredential credential = PasswordCredential.Create(Guid.NewGuid(), userId, Now);
            credential.CompleteSetup("$kalm$test", Now);
            user.Activate(credential, Now);
            identity.PasswordCredentials.Add(credential);
        }
        identity.Users.Add(user);
        identity.UserRoleAssignments.Add(UserRoleAssignment.Assign(Guid.NewGuid(), organizationId, userId, roleId, Now));
        await identity.SaveChangesAsync();
        return userId;
    }

    private static RoleAdministrationAuditTransactionCoordinator CreateCoordinator(string connectionString)
        => new(Options.Create(new DatabaseOptions { ConnectionString = connectionString }), new FixedClock(Now));

    private static async Task MigrateAsync(string connectionString)
    {
        await using var identity = CreateIdentity(connectionString);
        await identity.Database.MigrateAsync();
        await using var audit = CreateAudit(connectionString);
        await audit.Database.MigrateAsync();
    }

    private static IdentityDbContext CreateIdentity(string connectionString) => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "identity")).Options);
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
        private TestDatabase(string admin, string connectionString, string name) { _admin = admin; ConnectionString = connectionString; _name = name; }
        public string ConnectionString { get; }
        public static async Task<TestDatabase> CreateAsync()
        {
            string admin = Environment.GetEnvironmentVariable("KALM_TEST_POSTGRES_ADMIN") ?? DefaultAdmin;
            string name = $"kalm_role_tx_{Guid.NewGuid():N}";
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
