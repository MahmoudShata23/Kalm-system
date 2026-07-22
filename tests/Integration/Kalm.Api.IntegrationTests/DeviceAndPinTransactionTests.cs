using Kalm.Api.Configuration;
using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.DeviceAdministration;
using Kalm.Api.Transactions;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Application.ManagementAuthentication;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Identity.Infrastructure.Security;
using Kalm.Organization;
using Kalm.Organization.Domain;
using Kalm.Organization.Domain.ValueObjects;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OrganizationAggregate = Kalm.Organization.Domain.Organization;

namespace Kalm.Api.IntegrationTests;

public sealed class DeviceAndPinTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeviceCookie_IsHostOnlySecureHttpOnlyStrictBoundedAndClearedWhenInvalid()
    {
        var issued = new DefaultHttpContext();
        DeviceCredentialResolver.SetCookie(issued, "opaque-device-credential", Now);
        Assert.Single(issued.Response.Headers.SetCookie);
        string setCookie = issued.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{DeviceCredentialResolver.CookieName}=", setCookie, StringComparison.Ordinal);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=2592000", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);

        await using var database = await TestDatabase.CreateAsync(); await MigrateAsync(database.ConnectionString);
        await using var organization = Organization(database.ConnectionString);
        var invalid = new DefaultHttpContext();
        invalid.Request.Headers.Cookie = $"{DeviceCredentialResolver.CookieName}=invalid";
        Assert.Null(await new DeviceCredentialResolver(organization).ResolveAsync(invalid, CancellationToken.None));
        Assert.Single(invalid.Response.Headers.SetCookie);
        string cleared = invalid.Response.Headers.SetCookie.ToString();
        Assert.StartsWith($"{DeviceCredentialResolver.CookieName}=", cleared, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cleared, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PairingChallenge_IsHashedSingleUseAndConcurrentExchangeHasOneWinner()
    {
        await using var database = await TestDatabase.CreateAsync(); await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid branchId) = await SeedOrganizationAsync(database.ConnectionString);
        DeviceAdministrationAuditTransactionCoordinator coordinator = Devices(database.ConnectionString, Now);
        DeviceOperationResult created = await coordinator.CreateAsync(organizationId, Guid.NewGuid(), new DeviceCreateRequest(branchId, "Front POS", "posTerminal", "Browser"), "create", CancellationToken.None);
        DeviceChallengeResult challenge = await coordinator.CreateChallengeAsync(organizationId, Guid.NewGuid(), created.DeviceId, "challenge", CancellationToken.None);
        Assert.Equal(20, DecodeBase64Url(challenge.Challenge!).Length);

        DevicePairResult[] results = await Task.WhenAll(
            coordinator.PairAsync(created.DeviceId, challenge.Challenge!, "pair-one", CancellationToken.None),
            coordinator.PairAsync(created.DeviceId, challenge.Challenge!, "pair-two", CancellationToken.None));

        DevicePairResult winner = Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => !result.Succeeded);
        Assert.Equal(32, DecodeBase64Url(winner.Credential!).Length);
        await using OrganizationDbContext organization = Organization(database.ConnectionString);
        DevicePairingChallenge storedChallenge = await organization.DevicePairingChallenges.SingleAsync();
        DeviceCredential storedCredential = await organization.DeviceCredentials.SingleAsync(credential => credential.RevokedAtUtc == null);
        Assert.NotEqual(challenge.Challenge, storedChallenge.ChallengeHash);
        Assert.NotEqual(winner.Credential, storedCredential.CredentialHash);
        Assert.NotNull(storedChallenge.ConsumedAtUtc);
        Assert.Equal(DeviceStatus.Active, (await organization.Devices.SingleAsync()).Status);
        await using AuditDbContext audit = Audit(database.ConnectionString);
        var auditJson = await audit.AuditEntries.Select(entry => new { entry.BeforeJson, entry.AfterJson }).ToArrayAsync();
        string payload = string.Concat(auditJson.Select(entry => (entry.BeforeJson ?? "") + (entry.AfterJson ?? "")));
        Assert.DoesNotContain(challenge.Challenge!, payload, StringComparison.Ordinal);
        Assert.DoesNotContain(winner.Credential!, payload, StringComparison.Ordinal);

        DeviceChallengeResult invalidated = await Devices(database.ConnectionString, Now.AddMinutes(1)).CreateChallengeAsync(organizationId, Guid.NewGuid(), created.DeviceId, "invalidated", CancellationToken.None);
        DeviceChallengeResult replacement = await Devices(database.ConnectionString, Now.AddMinutes(2)).CreateChallengeAsync(organizationId, Guid.NewGuid(), created.DeviceId, "replacement", CancellationToken.None);
        Assert.False((await Devices(database.ConnectionString, Now.AddMinutes(2)).PairAsync(created.DeviceId, invalidated.Challenge!, "old-challenge", CancellationToken.None)).Succeeded);
        Assert.True((await Devices(database.ConnectionString, Now.AddMinutes(2)).PairAsync(created.DeviceId, replacement.Challenge!, "rotate", CancellationToken.None)).Succeeded);
        await using (OrganizationDbContext rotation = Organization(database.ConnectionString))
        {
            Assert.Equal(2, await rotation.DeviceCredentials.CountAsync());
            Assert.Single(await rotation.DeviceCredentials.Where(credential => credential.RevokedAtUtc == null).ToArrayAsync());
        }

        DeviceChallengeResult expiring = await Devices(database.ConnectionString, Now.AddMinutes(3)).CreateChallengeAsync(organizationId, Guid.NewGuid(), created.DeviceId, "expiring", CancellationToken.None);
        Assert.False((await Devices(database.ConnectionString, Now.AddMinutes(14)).PairAsync(created.DeviceId, expiring.Challenge!, "expired", CancellationToken.None)).Succeeded);
    }

    [Fact]
    public async Task PinResetConcurrentLoginLockoutAndDeviceRevocation_AreDatabaseAuthoritative()
    {
        await using var database = await TestDatabase.CreateAsync(); await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid branchId) = await SeedOrganizationAsync(database.ConnectionString);
        DeviceAdministrationAuditTransactionCoordinator deviceCoordinator = Devices(database.ConnectionString, Now);
        DeviceOperationResult created = await deviceCoordinator.CreateAsync(organizationId, Guid.NewGuid(), new DeviceCreateRequest(branchId, "Bar POS", "posTerminal", null), "create", CancellationToken.None);
        DeviceChallengeResult challenge = await deviceCoordinator.CreateChallengeAsync(organizationId, Guid.NewGuid(), created.DeviceId, "challenge", CancellationToken.None);
        Assert.True((await deviceCoordinator.PairAsync(created.DeviceId, challenge.Challenge!, "pair", CancellationToken.None)).Succeeded);
        (Guid userId, long userVersion) = await SeedEligibleUserAsync(database.ConnectionString, organizationId, branchId);

        var pinHasher = new Pbkdf2PinHasher(Options.Create(new PasswordHashingOptions()));
        var pinAdmin = new PinAdministrationAuditTransactionCoordinator(Options.Create(new DatabaseOptions { ConnectionString = database.ConnectionString }), pinHasher, new FixedClock(Now));
        PinAdministrationResult pinSet = await pinAdmin.SetAsync(organizationId, Guid.NewGuid(), userId, userVersion, "123456", "pin-set", CancellationToken.None);
        Assert.True(pinSet.Succeeded);
        await using (IdentityDbContext identity = Identity(database.ConnectionString))
        {
            PinCredential pin = await identity.PinCredentials.SingleAsync();
            Assert.NotEqual("123456", pin.EncodedHash);
            Assert.True(pinHasher.Verify("123456", pin.EncodedHash));
            Assert.All(await identity.UserSessions.ToArrayAsync(), session => Assert.Equal(SessionRevocationReason.PinChanged, session.RevocationReason));
        }

        DeviceRequestContext device;
        await using (OrganizationDbContext organization = Organization(database.ConnectionString))
        {
            Device stored = await organization.Devices.SingleAsync(); device = new(stored.Id, stored.OrganizationId, stored.BranchId, stored.SecurityVersion, stored.Name, stored.Type);
        }
        _ = await SeedEligibleUserAsync(database.ConnectionString, organizationId, branchId, "without-pin");
        await using (var identity = Identity(database.ConnectionString))
        await using (var organization = Organization(database.ConnectionString))
        {
            EligibleEmployeesResponse eligible = await new DeviceAuthenticationQueries(identity, organization).EligibleAsync(device, CancellationToken.None);
            EligibleEmployeeResponse only = Assert.Single(eligible.Items);
            Assert.Equal(userId, only.Id);
        }
        DevicePinLoginResult[] concurrentFailures = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(attempt => Authentication(database.ConnectionString, Now, pinHasher).LoginAsync(device, userId, "654321", $"bad-{attempt}", CancellationToken.None)));
        Assert.All(concurrentFailures, result => Assert.False(result.Succeeded));
        await using (var identity = Identity(database.ConnectionString)) Assert.Equal(5, await identity.PinLoginAttempts.CountAsync(attempt => attempt.UserId == userId));
        Assert.False((await Authentication(database.ConnectionString, Now, pinHasher).LoginAsync(device, userId, "123456", "locked", CancellationToken.None)).Succeeded);
        DevicePinLoginResult login = await Authentication(database.ConnectionString, Now.AddMinutes(16), pinHasher).LoginAsync(device, userId, "123456", "success", CancellationToken.None);
        Assert.True(login.Succeeded);

        await using (IdentityDbContext identity = Identity(database.ConnectionString))
        {
            UserSession session = await identity.UserSessions.SingleAsync(candidate => candidate.Id == login.SessionId);
            Assert.Equal(device.DeviceId, session.DeviceId); Assert.Equal(branchId, session.BranchId);
        }
        Assert.True(await Authentication(database.ConnectionString, Now.AddMinutes(17), pinHasher).LockAsync(login.SessionId, "lock", CancellationToken.None));
        await using (var organization = Organization(database.ConnectionString)) Assert.Single(await organization.DeviceCredentials.Where(credential => credential.RevokedAtUtc == null).ToArrayAsync());
        DevicePinLoginResult switched = await Authentication(database.ConnectionString, Now.AddMinutes(18), pinHasher).LoginAsync(device, userId, "123456", "switch", CancellationToken.None);
        Assert.True(switched.Succeeded);
        await using (OrganizationDbContext organization = Organization(database.ConnectionString))
        {
            Device stored = await organization.Devices.SingleAsync();
            Assert.True((await Devices(database.ConnectionString, Now.AddMinutes(19)).RevokeAsync(organizationId, Guid.NewGuid(), stored.Id, stored.Version, "revoke", CancellationToken.None)).Succeeded);
        }
        await using (IdentityDbContext identity = Identity(database.ConnectionString))
        {
            Assert.Equal(SessionRevocationReason.WorkstationLocked, (await identity.UserSessions.SingleAsync(candidate => candidate.Id == login.SessionId)).RevocationReason);
            Assert.Equal(SessionRevocationReason.DeviceRevoked, (await identity.UserSessions.SingleAsync(candidate => candidate.Id == switched.SessionId)).RevocationReason);
        }
    }

    [Fact]
    public async Task DeviceUpdate_EnforcesOrganizationBranchAndEtagAndInvalidatesPairing()
    {
        await using var database = await TestDatabase.CreateAsync(); await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid branchId) = await SeedOrganizationAsync(database.ConnectionString);
        Guid otherOrganizationId = Guid.NewGuid(), otherBranchId = Guid.NewGuid();
        DeviceAdministrationAuditTransactionCoordinator coordinator = Devices(database.ConnectionString, Now);
        DeviceOperationResult created = await coordinator.CreateAsync(organizationId, Guid.NewGuid(), new DeviceCreateRequest(branchId, "Kitchen POS", "posTerminal", "Windows"), "create", CancellationToken.None);
        DeviceChallengeResult challenge = await coordinator.CreateChallengeAsync(organizationId, Guid.NewGuid(), created.DeviceId, "challenge", CancellationToken.None);
        Assert.True((await coordinator.PairAsync(created.DeviceId, challenge.Challenge!, "pair", CancellationToken.None)).Succeeded);
        await using var organization = Organization(database.ConnectionString);
        Device paired = await organization.Devices.SingleAsync(device => device.Id == created.DeviceId);

        DeviceOperationResult isolated = await coordinator.UpdateAsync(otherOrganizationId, Guid.NewGuid(), paired.Id, paired.Version, new DeviceUpdateRequest(otherBranchId, paired.Name, "posTerminal", "Linux"), "isolated", CancellationToken.None);
        Assert.Equal("device.not_found", isolated.ErrorCode);
        DeviceOperationResult stale = await coordinator.UpdateAsync(organizationId, Guid.NewGuid(), paired.Id, paired.Version - 1, new DeviceUpdateRequest(branchId, paired.Name, "posTerminal", "Linux"), "stale", CancellationToken.None);
        Assert.Equal("device.concurrency_conflict", stale.ErrorCode);
        Assert.Equal(paired.Version, stale.CurrentVersion);
        DeviceOperationResult crossTenantBranch = await coordinator.UpdateAsync(organizationId, Guid.NewGuid(), paired.Id, paired.Version, new DeviceUpdateRequest(otherBranchId, paired.Name, "posTerminal", "Linux"), "branch", CancellationToken.None);
        Assert.Equal("device.branch_invalid", crossTenantBranch.ErrorCode);
        DeviceOperationResult updated = await coordinator.UpdateAsync(organizationId, Guid.NewGuid(), paired.Id, paired.Version, new DeviceUpdateRequest(branchId, paired.Name, "posTerminal", "Linux"), "update", CancellationToken.None);
        Assert.True(updated.Succeeded);

        organization.ChangeTracker.Clear();
        Device reloaded = await organization.Devices.SingleAsync(device => device.Id == created.DeviceId);
        Assert.Equal(DeviceStatus.PendingPairing, reloaded.Status);
        Assert.Empty(await organization.DeviceCredentials.Where(credential => credential.DeviceId == created.DeviceId && credential.RevokedAtUtc == null).ToArrayAsync());
    }

    [Fact]
    public async Task SliceSixMutations_RollBackWhenSuccessAuditCannotBeWritten()
    {
        await using var database = await TestDatabase.CreateAsync(); await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid branchId) = await SeedOrganizationAsync(database.ConnectionString);
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand("create function audit.reject_slice6_success() returns trigger language plpgsql as $$ begin if new.action in ('DeviceRegistered', 'UserPinSet') then raise exception 'forced slice6 audit failure'; end if; return new; end; $$; create trigger trg_reject_slice6_success before insert on audit.audit_logs for each row execute function audit.reject_slice6_success();", connection).ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => Devices(database.ConnectionString, Now).CreateAsync(organizationId, Guid.NewGuid(), new DeviceCreateRequest(branchId, "Rollback POS", "posTerminal", null), "rollback-device", CancellationToken.None));
        await using (var organization = Organization(database.ConnectionString)) Assert.Empty(await organization.Devices.ToArrayAsync());

        (Guid userId, long version) = await SeedEligibleUserAsync(database.ConnectionString, organizationId, branchId, "rollback-pin");
        var hasher = new Pbkdf2PinHasher(Options.Create(new PasswordHashingOptions()));
        var pinAdmin = new PinAdministrationAuditTransactionCoordinator(Options.Create(new DatabaseOptions { ConnectionString = database.ConnectionString }), hasher, new FixedClock(Now));
        await Assert.ThrowsAsync<DbUpdateException>(() => pinAdmin.SetAsync(organizationId, Guid.NewGuid(), userId, version, "123456", "rollback-pin", CancellationToken.None));
        await using (var identity = Identity(database.ConnectionString))
        {
            Assert.Empty(await identity.PinCredentials.Where(credential => credential.UserId == userId).ToArrayAsync());
            Assert.All(await identity.UserSessions.Where(session => session.UserId == userId).ToArrayAsync(), session => Assert.Null(session.RevokedAtUtc));
        }
    }

    [Fact]
    public async Task DeviceRevocationAndPinReset_RollBackCredentialAndSessionRevocationWhenAuditFails()
    {
        await using var database = await TestDatabase.CreateAsync(); await MigrateAsync(database.ConnectionString);
        (Guid organizationId, Guid branchId) = await SeedOrganizationAsync(database.ConnectionString);
        DeviceAdministrationAuditTransactionCoordinator devices = Devices(database.ConnectionString, Now);
        DeviceOperationResult created = await devices.CreateAsync(organizationId, Guid.NewGuid(), new DeviceCreateRequest(branchId, "Rollback Security POS", "posTerminal", null), "create", CancellationToken.None);
        DeviceChallengeResult challenge = await devices.CreateChallengeAsync(organizationId, Guid.NewGuid(), created.DeviceId, "challenge", CancellationToken.None);
        Assert.True((await devices.PairAsync(created.DeviceId, challenge.Challenge!, "pair", CancellationToken.None)).Succeeded);
        (Guid userId, long userVersion) = await SeedEligibleUserAsync(database.ConnectionString, organizationId, branchId, "rollback-security");
        var hasher = new Pbkdf2PinHasher(Options.Create(new PasswordHashingOptions()));
        var pinAdmin = new PinAdministrationAuditTransactionCoordinator(Options.Create(new DatabaseOptions { ConnectionString = database.ConnectionString }), hasher, new FixedClock(Now));
        PinAdministrationResult initialPin = await pinAdmin.SetAsync(organizationId, Guid.NewGuid(), userId, userVersion, "123456", "initial-pin", CancellationToken.None);
        Assert.True(initialPin.Succeeded);

        DeviceRequestContext device;
        long deviceVersion;
        string credentialHash;
        await using (var organization = Organization(database.ConnectionString))
        {
            Device stored = await organization.Devices.SingleAsync();
            device = new(stored.Id, stored.OrganizationId, stored.BranchId, stored.SecurityVersion, stored.Name, stored.Type);
            deviceVersion = stored.Version;
            credentialHash = (await organization.DeviceCredentials.SingleAsync(credential => credential.RevokedAtUtc == null)).CredentialHash;
        }
        DevicePinLoginResult login = await Authentication(database.ConnectionString, Now, hasher).LoginAsync(device, userId, "123456", "login", CancellationToken.None);
        Assert.True(login.Succeeded);
        string originalPinHash;
        await using (var identity = Identity(database.ConnectionString)) originalPinHash = (await identity.PinCredentials.SingleAsync(credential => credential.UserId == userId)).EncodedHash;

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand("create function audit.reject_slice6_revocation() returns trigger language plpgsql as $$ begin if new.action in ('DeviceRevoked', 'UserPinReset') then raise exception 'forced slice6 revocation audit failure'; end if; return new; end; $$; create trigger trg_reject_slice6_revocation before insert on audit.audit_logs for each row execute function audit.reject_slice6_revocation();", connection).ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => Devices(database.ConnectionString, Now.AddMinutes(1)).RevokeAsync(organizationId, Guid.NewGuid(), created.DeviceId, deviceVersion, "rollback-revoke", CancellationToken.None));
        await Assert.ThrowsAsync<DbUpdateException>(() => new PinAdministrationAuditTransactionCoordinator(Options.Create(new DatabaseOptions { ConnectionString = database.ConnectionString }), hasher, new FixedClock(Now.AddMinutes(2))).SetAsync(organizationId, Guid.NewGuid(), userId, initialPin.Version, "654321", "rollback-reset", CancellationToken.None));

        await using (var organization = Organization(database.ConnectionString))
        {
            Assert.Equal(DeviceStatus.Active, (await organization.Devices.SingleAsync()).Status);
            DeviceCredential activeCredential = await organization.DeviceCredentials.SingleAsync(credential => credential.RevokedAtUtc == null);
            Assert.Equal(credentialHash, activeCredential.CredentialHash);
        }
        await using (var identity = Identity(database.ConnectionString))
        {
            Assert.Null((await identity.UserSessions.SingleAsync(session => session.Id == login.SessionId)).RevokedAtUtc);
            PinCredential pin = await identity.PinCredentials.SingleAsync(credential => credential.UserId == userId);
            Assert.Equal(originalPinHash, pin.EncodedHash);
            Assert.True(hasher.Verify("123456", pin.EncodedHash));
            Assert.False(hasher.Verify("654321", pin.EncodedHash));
        }
    }

    private static DeviceAdministrationAuditTransactionCoordinator Devices(string cs, DateTimeOffset now) => new(Options.Create(new DatabaseOptions { ConnectionString = cs }), new FixedClock(now));
    private static DeviceAuthenticationAuditTransactionCoordinator Authentication(string cs, DateTimeOffset now, Pbkdf2PinHasher hasher) => new(Options.Create(new DatabaseOptions { ConnectionString = cs }), hasher, new DummyPinHash(hasher), new FixedClock(now), Options.Create(new ManagementAuthenticationOptions()));
    private static async Task MigrateAsync(string cs) { await using var o = Organization(cs); await o.Database.MigrateAsync(); await using var i = Identity(cs); await i.Database.MigrateAsync(); await using var a = Audit(cs); await a.Database.MigrateAsync(); }
    private static async Task<(Guid OrganizationId, Guid BranchId)> SeedOrganizationAsync(string cs)
    {
        Guid organizationId = Guid.NewGuid(), branchId = Guid.NewGuid(); await using var db = Organization(cs); OrganizationAggregate tenant = OrganizationAggregate.Create(organizationId, new OrganizationName("Kalm", 120), null, new CurrencyCode("EGP"), new LocaleCode("en"), Now); tenant.ChangeStatus(OrganizationStatus.Active, Now); Branch branch = Branch.Create(branchId, organizationId, new OrganizationName("Cairo", 120), new BranchCode("CAI"), new LocaleCode("en"), new TimeZoneId("Africa/Cairo"), new BusinessDayRollover(new TimeOnly(4, 0)), Now); branch.ChangeStatus(BranchStatus.Active, Now); db.AddRange(tenant, branch); await db.SaveChangesAsync(); return (organizationId, branchId);
    }
    private static async Task<(Guid UserId, long Version)> SeedEligibleUserAsync(string cs, Guid organizationId, Guid branchId, string username = "employee")
    {
        Guid userId = Guid.NewGuid(), roleId = Guid.NewGuid(); await using (var identity = Identity(cs)) { var hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions())); User user = User.Create(userId, organizationId, new Username(username), null, new DisplayName(username), "en", Now); PasswordCredential password = PasswordCredential.Create(Guid.NewGuid(), userId, Now); password.CompleteSetup(hasher.Hash("a secure employee password"), Now); user.Activate(password, Now); Role role = Role.Create(roleId, organizationId, new RoleName($"{username} role"), null, Now); Permission permission = await identity.Permissions.FirstAsync(); identity.AddRange(user, password, role, RolePermission.Grant(Guid.NewGuid(), roleId, permission.Id, Now), UserRoleAssignment.Assign(Guid.NewGuid(), organizationId, userId, roleId, Now), UserSession.Create(Guid.NewGuid(), userId, Now, TimeSpan.FromMinutes(20), TimeSpan.FromHours(8))); await identity.SaveChangesAsync(); }
        await using (var organization = Organization(cs)) { UserBranchAccess access = UserBranchAccess.Create(Guid.NewGuid(), organizationId, userId, BranchAccessScope.AssignedBranches, Now); organization.AddRange(access, UserBranchAssignment.Assign(Guid.NewGuid(), access.Id, organizationId, branchId, Now)); await organization.SaveChangesAsync(); }
        await using var check = Identity(cs); return (userId, (await check.Users.SingleAsync(user => user.Id == userId)).Version);
    }
    private static OrganizationDbContext Organization(string cs) => new(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(cs, o => o.MigrationsHistoryTable("__ef_migrations_history", "organization")).Options);
    private static IdentityDbContext Identity(string cs) => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(cs, o => o.MigrationsHistoryTable("__ef_migrations_history", "identity")).Options);
    private static AuditDbContext Audit(string cs) => new(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(cs, o => o.MigrationsHistoryTable("__ef_migrations_history", "audit")).Options);
    private static byte[] DecodeBase64Url(string value) { string base64 = value.Replace('-', '+').Replace('_', '/'); return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=')); }
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; } = now; }
    private sealed class TestDatabase : IAsyncDisposable { private const string DefaultAdmin = "Host=localhost;Port=54329;Database=postgres;Username=kalm;Password=kalm_dev_password"; private readonly string admin, name; private TestDatabase(string a, string c, string n) { admin = a; ConnectionString = c; name = n; } public string ConnectionString { get; } public static async Task<TestDatabase> CreateAsync() { string a = Environment.GetEnvironmentVariable("KALM_TEST_POSTGRES_ADMIN") ?? DefaultAdmin, n = $"kalm_device_{Guid.NewGuid():N}"; await using var c = new NpgsqlConnection(a); await c.OpenAsync(); await new NpgsqlCommand($"create database \"{n}\"", c).ExecuteNonQueryAsync(); return new(a, new NpgsqlConnectionStringBuilder(a) { Database = n }.ConnectionString, n); } public async ValueTask DisposeAsync() { NpgsqlConnection.ClearAllPools(); await using var c = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(admin) { Pooling = false }.ConnectionString); await c.OpenAsync(); await new NpgsqlCommand($"drop database if exists \"{name}\" with (force)", c).ExecuteNonQueryAsync(); } }
}
