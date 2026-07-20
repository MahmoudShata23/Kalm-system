using Kalm.Api.Configuration;
using Kalm.Api.Persistence;
using Kalm.Api.Transactions;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Domain.ValueObjects;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kalm.Api.IntegrationTests;

public sealed class MilestoneOneAMigrationTests
{
    private const string FoundationMigration = "20260715140000_InitialFoundation";
    private const string OrganizationMigration = "20260720181706_AddOrganizationFoundation";
    private const string AuditMigration = "20260720181822_AddAuditFoundation";

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
        Assert.Equal("trg_audit_logs_immutable", await ResolveTriggerAsync(database.ConnectionString));
        Assert.Equal("organization.ux_organizations_singleton_key", await ResolveRegClassAsync(database.ConnectionString, "organization.ux_organizations_singleton_key"));
        Assert.Equal("organization.ux_branches_organization_id_code", await ResolveRegClassAsync(database.ConnectionString, "organization.ux_branches_organization_id_code"));
        Assert.Equal("audit.ix_audit_logs_entity_type_entity_id_occurred_at_utc", await ResolveRegClassAsync(database.ConnectionString, "audit.ix_audit_logs_entity_type_entity_id_occurred_at_utc"));
        Assert.True(await ConstraintExistsAsync(database.ConnectionString, "organization", "organizations", "ck_organizations_singleton_key"));
        Assert.True(await ConstraintExistsAsync(database.ConnectionString, "audit", "audit_logs", "ck_audit_logs_action"));

        await using var platform = CreatePlatformContext(database.ConnectionString);
        await using var organization = CreateOrganizationContext(database.ConnectionString);
        await using var audit = CreateAuditContext(database.ConnectionString);
        Assert.Equal([FoundationMigration], await platform.Database.GetAppliedMigrationsAsync());
        Assert.Equal([OrganizationMigration], await organization.Database.GetAppliedMigrationsAsync());
        Assert.Equal([AuditMigration], await audit.Database.GetAppliedMigrationsAsync());
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
    }

    private static KalmDbContext CreatePlatformContext(string connectionString) => new(new DbContextOptionsBuilder<KalmDbContext>().UseNpgsql(connectionString).Options);
    private static OrganizationDbContext CreateOrganizationContext(string connectionString) => new(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "organization")).Options);
    private static AuditDbContext CreateAuditContext(string connectionString) => new(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "audit")).Options);

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
