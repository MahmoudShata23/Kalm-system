using Kalm.Api.Configuration;
using Kalm.Api.Transactions;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization;
using Kalm.Organization.Domain;
using Kalm.Organization.Domain.ValueObjects;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OrganizationAggregate = Kalm.Organization.Domain.Organization;

namespace Kalm.Api.IntegrationTests;

public sealed class BranchAdministrationTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreationUpdateNoOpAndIsolation_AreAtomicAndVersioned()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = await SeedOrganizationAsync(database.ConnectionString);
        Guid actorId = Guid.NewGuid();
        BranchAdministrationAuditTransactionCoordinator coordinator = Branches(database.ConnectionString, Now);

        BranchOperationResult created = await coordinator.CreateAsync(
            organizationId,
            actorId,
            new BranchWriteRequest(" Cairo West ", " cw-01 ", "ar-EG", "Africa/Cairo", "04:00"),
            "branch-create",
            CancellationToken.None);
        Assert.True(created.Succeeded);
        Assert.Equal(1, created.Version);

        BranchOperationResult isolated = await coordinator.UpdateAsync(
            Guid.NewGuid(), actorId, created.BranchId, created.Version,
            new BranchWriteRequest("Hidden", "HID", "en", "Africa/Cairo", "04:00"),
            "branch-isolation", CancellationToken.None);
        Assert.Equal("branch.not_found", isolated.ErrorCode);

        BranchOperationResult noOp = await coordinator.UpdateAsync(
            organizationId, actorId, created.BranchId, created.Version,
            new BranchWriteRequest("Cairo West", "CW-01", "ar-EG", "Africa/Cairo", "04:00"),
            "branch-noop", CancellationToken.None);
        Assert.True(noOp.Succeeded);
        Assert.Equal(created.Version, noOp.Version);

        BranchOperationResult stale = await coordinator.UpdateAsync(
            organizationId, actorId, created.BranchId, created.Version + 1,
            new BranchWriteRequest("Cairo West Two", "CW-01", "ar-EG", "Africa/Cairo", "05:00"),
            "branch-stale", CancellationToken.None);
        Assert.Equal("branch.concurrency_conflict", stale.ErrorCode);
        Assert.Equal(created.Version, stale.CurrentVersion);

        BranchOperationResult updated = await coordinator.UpdateAsync(
            organizationId, actorId, created.BranchId, created.Version,
            new BranchWriteRequest("Cairo West Two", "CW-01", "en", "Africa/Cairo", "05:00"),
            "branch-update", CancellationToken.None);
        Assert.True(updated.Succeeded);
        Assert.Equal(2, updated.Version);

        await using var organization = Organization(database.ConnectionString);
        Branch branch = await organization.Branches.SingleAsync(candidate => candidate.Id == created.BranchId);
        Assert.Equal("Cairo West Two", branch.Name);
        Assert.Equal("CW-01", branch.Code);
        Assert.Equal(new TimeOnly(5, 0), branch.BusinessDayRollover);
        await using var audit = Audit(database.ConnectionString);
        Assert.Equal(2, await audit.AuditEntries.CountAsync(entry => entry.EntityId == branch.Id));
        Assert.Contains(await audit.AuditEntries.ToArrayAsync(), entry => entry.Action == AuditAction.BranchCreated);
        Assert.Contains(await audit.AuditEntries.ToArrayAsync(), entry => entry.Action == AuditAction.BranchUpdated);
    }

    [Fact]
    public async Task Deactivation_BlocksActiveDevicesAndExplicitAssignmentsWithoutMutation()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = await SeedOrganizationAsync(database.ConnectionString);
        BranchAdministrationAuditTransactionCoordinator branches = Branches(database.ConnectionString, Now);
        BranchOperationResult created = await branches.CreateAsync(organizationId, Guid.NewGuid(), Request("Dependencies", "DEP"), "create", CancellationToken.None);
        BranchOperationResult activated = await branches.ActivateAsync(organizationId, Guid.NewGuid(), created.BranchId, created.Version, "activate", CancellationToken.None);
        DeviceAdministrationAuditTransactionCoordinator devices = Devices(database.ConnectionString, Now.AddMinutes(1));
        DeviceOperationResult device = await devices.CreateAsync(organizationId, Guid.NewGuid(), new DeviceCreateRequest(created.BranchId, "Dependency POS", "posTerminal", null), "device", CancellationToken.None);
        Assert.True(device.Succeeded);
        DeviceChallengeResult challenge = await devices.CreateChallengeAsync(organizationId, Guid.NewGuid(), device.DeviceId, "challenge", CancellationToken.None);
        Assert.True((await devices.PairAsync(device.DeviceId, challenge.Challenge!, "pair", CancellationToken.None)).Succeeded);

        Guid userId = Guid.NewGuid();
        await using (var organization = Organization(database.ConnectionString))
        {
            UserBranchAccess access = UserBranchAccess.Create(Guid.NewGuid(), organizationId, userId, BranchAccessScope.AssignedBranches, Now);
            organization.AddRange(access, UserBranchAssignment.Assign(Guid.NewGuid(), access.Id, organizationId, created.BranchId, Now));
            await organization.SaveChangesAsync();
        }
        await using (var organization = Organization(database.ConnectionString))
        await using (var identity = Identity(database.ConnectionString))
        {
            Device pairedDevice = await organization.Devices.SingleAsync(candidate => candidate.Id == device.DeviceId);
            User user = User.Create(userId, organizationId, new Username("branch-dependent-user"), null, new DisplayName("Branch Dependent User"), "en", Now);
            identity.AddRange(
                user,
                UserSession.CreateDeviceBound(
                    Guid.NewGuid(), userId, pairedDevice.Id, created.BranchId, pairedDevice.SecurityVersion, 1, user.AuthorizationVersion,
                    Now, TimeSpan.FromMinutes(20), TimeSpan.FromHours(8)));
            await identity.SaveChangesAsync();
        }

        BranchOperationResult rejected = await branches.DeactivateAsync(organizationId, Guid.NewGuid(), created.BranchId, activated.Version, "deactivate", CancellationToken.None);
        Assert.Equal("branch.dependencies_active", rejected.ErrorCode);
        Assert.NotNull(rejected.Dependencies);
        Assert.Equal(0, rejected.Dependencies.RegisteredDeviceCount);
        Assert.Equal(1, rejected.Dependencies.ActiveDeviceCount);
        Assert.Equal(1, rejected.Dependencies.ActiveCredentialCount);
        Assert.Equal(1, rejected.Dependencies.ActiveSessionCount);
        Assert.Equal(1, rejected.Dependencies.ActiveUserAssignmentCount);

        await using (var organization = Organization(database.ConnectionString))
        {
            Assert.Equal(BranchStatus.Active, (await organization.Branches.SingleAsync(branch => branch.Id == created.BranchId)).Status);
            Assert.Equal(DeviceStatus.Active, (await organization.Devices.SingleAsync(candidate => candidate.Id == device.DeviceId)).Status);
            Assert.Null((await organization.DeviceCredentials.SingleAsync()).RevokedAtUtc);
            Assert.Null((await organization.UserBranchAssignments.SingleAsync()).RevokedAtUtc);
        }
        await using (var identity = Identity(database.ConnectionString)) Assert.Null((await identity.UserSessions.SingleAsync()).RevokedAtUtc);
        await using var audit = Audit(database.ConnectionString);
        AuditEntry denial = await audit.AuditEntries.SingleAsync(entry => entry.Action == AuditAction.BranchAdministrationRejected);
        Assert.Equal(AuditResult.Denied, denial.Result);
        Assert.DoesNotContain(userId.ToString(), denial.AfterJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivationAndDeactivation_ChangeOnlyBranchStatus()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = await SeedOrganizationAsync(database.ConnectionString);
        BranchAdministrationAuditTransactionCoordinator coordinator = Branches(database.ConnectionString, Now);
        BranchOperationResult created = await coordinator.CreateAsync(organizationId, Guid.NewGuid(), Request("Lifecycle", "LIFE"), "create", CancellationToken.None);

        BranchOperationResult active = await coordinator.ActivateAsync(organizationId, Guid.NewGuid(), created.BranchId, created.Version, "activate", CancellationToken.None);
        BranchOperationResult suspended = await coordinator.DeactivateAsync(organizationId, Guid.NewGuid(), created.BranchId, active.Version, "deactivate", CancellationToken.None);
        BranchOperationResult restored = await coordinator.ActivateAsync(organizationId, Guid.NewGuid(), created.BranchId, suspended.Version, "reactivate", CancellationToken.None);

        Assert.Equal(4, restored.Version);
        await using var organization = Organization(database.ConnectionString);
        Branch branch = await organization.Branches.SingleAsync(candidate => candidate.Id == created.BranchId);
        Assert.Equal(BranchStatus.Active, branch.Status);
        Assert.Equal("Lifecycle", branch.Name);
        Assert.Empty(await organization.Devices.Where(device => device.BranchId == branch.Id).ToArrayAsync());
        Assert.Empty(await organization.UserBranchAssignments.Where(assignment => assignment.BranchId == branch.Id).ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentDeviceRegistrationAndDeactivation_CannotBothCommit()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = await SeedOrganizationAsync(database.ConnectionString);
        BranchAdministrationAuditTransactionCoordinator branches = Branches(database.ConnectionString, Now);
        BranchOperationResult created = await branches.CreateAsync(organizationId, Guid.NewGuid(), Request("Concurrent", "CON"), "create", CancellationToken.None);
        BranchOperationResult active = await branches.ActivateAsync(organizationId, Guid.NewGuid(), created.BranchId, created.Version, "activate", CancellationToken.None);

        Task<BranchOperationResult> deactivationTask = branches.DeactivateAsync(organizationId, Guid.NewGuid(), created.BranchId, active.Version, "deactivate", CancellationToken.None);
        Task<DeviceOperationResult> registrationTask = Devices(database.ConnectionString, Now).CreateAsync(
            organizationId, Guid.NewGuid(), new DeviceCreateRequest(created.BranchId, "Concurrent POS", "posTerminal", null), "device", CancellationToken.None);
        await Task.WhenAll(deactivationTask, registrationTask);

        BranchOperationResult deactivation = await deactivationTask;
        DeviceOperationResult registration = await registrationTask;
        Assert.False(deactivation.Succeeded && registration.Succeeded);
        await using var organization = Organization(database.ConnectionString);
        Branch branch = await organization.Branches.SingleAsync(candidate => candidate.Id == created.BranchId);
        if (deactivation.Succeeded)
        {
            Assert.Equal(BranchStatus.Suspended, branch.Status);
            Assert.Empty(await organization.Devices.Where(device => device.BranchId == branch.Id).ToArrayAsync());
            Assert.Equal("device.branch_invalid", registration.ErrorCode);
        }
        else
        {
            Assert.Equal("branch.dependencies_active", deactivation.ErrorCode);
            Assert.Equal(BranchStatus.Active, branch.Status);
            Assert.True(registration.Succeeded);
        }
    }

    [Fact]
    public async Task BranchMutation_RollsBackWhenAuditWriteFails()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateAsync(database.ConnectionString);
        Guid organizationId = await SeedOrganizationAsync(database.ConnectionString);
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand("create function audit.reject_branch_success() returns trigger language plpgsql as $$ begin if new.action in ('BranchCreated', 'BranchUpdated', 'BranchDeactivated') then raise exception 'forced branch audit failure'; end if; return new; end; $$; create trigger trg_reject_branch_success before insert on audit.audit_logs for each row execute function audit.reject_branch_success();", connection).ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => Branches(database.ConnectionString, Now).CreateAsync(
            organizationId, Guid.NewGuid(), Request("Rollback", "RBK"), "rollback", CancellationToken.None));
        await using var organization = Organization(database.ConnectionString);
        Assert.Empty(await organization.Branches.Where(branch => branch.Code == "RBK").ToArrayAsync());
    }

    private static BranchWriteRequest Request(string name, string code)
        => new(name, code, "en", "Africa/Cairo", "04:00");

    private static BranchAdministrationAuditTransactionCoordinator Branches(string connectionString, DateTimeOffset now)
        => new(Options.Create(new DatabaseOptions { ConnectionString = connectionString }), new FixedClock(now));

    private static DeviceAdministrationAuditTransactionCoordinator Devices(string connectionString, DateTimeOffset now)
        => new(Options.Create(new DatabaseOptions { ConnectionString = connectionString }), new FixedClock(now));

    private static async Task MigrateAsync(string connectionString)
    {
        await using var organization = Organization(connectionString);
        await organization.Database.MigrateAsync();
        await using var identity = Identity(connectionString);
        await identity.Database.MigrateAsync();
        await using var audit = Audit(connectionString);
        await audit.Database.MigrateAsync();
    }

    private static async Task<Guid> SeedOrganizationAsync(string connectionString)
    {
        Guid organizationId = Guid.NewGuid();
        await using var context = Organization(connectionString);
        OrganizationAggregate organization = OrganizationAggregate.Create(
            organizationId,
            new OrganizationName("Kalm", 120),
            null,
            new CurrencyCode("EGP"),
            new LocaleCode("en"),
            Now);
        organization.ChangeStatus(OrganizationStatus.Active, Now);
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organizationId;
    }

    private static OrganizationDbContext Organization(string connectionString)
        => new(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "organization")).Options);

    private static IdentityDbContext Identity(string connectionString)
        => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "identity")).Options);

    private static AuditDbContext Audit(string connectionString)
        => new(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "audit")).Options);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private const string DefaultAdmin = "Host=localhost;Port=54329;Database=postgres;Username=kalm;Password=kalm_dev_password";
        private readonly string _admin;
        private readonly string _databaseName;

        private TestDatabase(string admin, string connectionString, string databaseName)
        {
            _admin = admin;
            ConnectionString = connectionString;
            _databaseName = databaseName;
        }

        public string ConnectionString { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            string admin = Environment.GetEnvironmentVariable("KALM_TEST_POSTGRES_ADMIN") ?? DefaultAdmin;
            string databaseName = $"kalm_branch_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await new NpgsqlCommand($"create database \"{databaseName}\"", connection).ExecuteNonQueryAsync();
            return new TestDatabase(admin, new NpgsqlConnectionStringBuilder(admin) { Database = databaseName }.ConnectionString, databaseName);
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_admin) { Pooling = false }.ConnectionString);
            await connection.OpenAsync();
            await new NpgsqlCommand($"drop database if exists \"{_databaseName}\" with (force)", connection).ExecuteNonQueryAsync();
        }
    }
}
