using System.Text.Json;
using Kalm.Api.Features.AuditViewer;
using Kalm.Api.Features.Authorization;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Domain.ValueObjects;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrganizationAggregate = Kalm.Organization.Domain.Organization;

namespace Kalm.Api.IntegrationTests;

public sealed class AuditViewerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task KeysetPagination_IsStableForEqualTimestampsAndRejectsTamperedOrScopeChangedCursors()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Seed seed = await SeedAsync(database.ConnectionString);
        await using AuditDbContext audit = Audit(database.ConnectionString);
        await using IdentityDbContext identity = Identity(database.ConnectionString);
        await using OrganizationDbContext organization = Organization(database.ConnectionString);
        var queries = Queries(audit, identity, organization);
        EffectiveBranchAccessSnapshot all = Access("allOrganizationBranches", seed.Branch1, seed.Branch2);
        AuditViewerFilter filter = Filter(pageSize: 2);

        var ids = new List<Guid>();
        AuditLogListResponse first = await queries.ListAsync(seed.OrganizationId, all, filter, CancellationToken.None);
        AuditLogListResponse page = first;
        while (true)
        {
            ids.AddRange(page.Items.Select(item => item.Id));
            if (page.NextCursor is null) break;
            page = await queries.ListAsync(seed.OrganizationId, all, filter with { Cursor = page.NextCursor }, CancellationToken.None);
        }

        Guid[] expected = await audit.AuditEntries.AsNoTracking()
            .Where(entry => entry.OrganizationId == seed.OrganizationId)
            .OrderByDescending(entry => entry.OccurredAtUtc).ThenByDescending(entry => entry.Id)
            .Select(entry => entry.Id).ToArrayAsync();
        Assert.Equal(expected, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());

        AuditLogListResponse second = await queries.ListAsync(seed.OrganizationId, all, filter with { Cursor = first.NextCursor }, CancellationToken.None);
        Assert.NotNull(second.PreviousCursor);
        AuditLogListResponse returned = await queries.ListAsync(seed.OrganizationId, all, filter with { Cursor = second.PreviousCursor }, CancellationToken.None);
        Assert.Equal(first.Items.Select(item => item.Id), returned.Items.Select(item => item.Id));

        string tampered = first.NextCursor![..^1] + (first.NextCursor[^1] == 'A' ? 'B' : 'A');
        AuditViewerQueryException malformed = await Assert.ThrowsAsync<AuditViewerQueryException>(() =>
            queries.ListAsync(seed.OrganizationId, all, filter with { Cursor = tampered }, CancellationToken.None));
        Assert.Equal("audit.invalid_cursor", malformed.Code);

        EffectiveBranchAccessSnapshot assigned = Access("assignedBranches", seed.Branch1);
        AuditViewerQueryException scopeChanged = await Assert.ThrowsAsync<AuditViewerQueryException>(() =>
            queries.ListAsync(seed.OrganizationId, assigned, filter with { Cursor = first.NextCursor }, CancellationToken.None));
        Assert.Equal("audit.invalid_cursor", scopeChanged.Code);
    }

    [Fact]
    public async Task OrganizationAndBranchScope_FiltersAndDetailsFailClosedWithoutWritingAuditRows()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Seed seed = await SeedAsync(database.ConnectionString);
        await using AuditDbContext audit = Audit(database.ConnectionString);
        await using IdentityDbContext identity = Identity(database.ConnectionString);
        await using OrganizationDbContext organization = Organization(database.ConnectionString);
        var queries = Queries(audit, identity, organization);
        EffectiveBranchAccessSnapshot assigned = Access("assignedBranches", seed.Branch1);
        int beforeCount = await audit.AuditEntries.CountAsync();

        AuditLogListResponse list = await queries.ListAsync(seed.OrganizationId, assigned, Filter(), CancellationToken.None);
        Assert.NotEmpty(list.Items);
        Assert.All(list.Items, item => Assert.Equal(seed.Branch1, item.Branch?.Id));
        Assert.Null(await queries.GetAsync(seed.OrganizationId, assigned, seed.Branch2AuditId, CancellationToken.None));
        Assert.Null(await queries.GetAsync(seed.OrganizationId, assigned, seed.BranchlessAuditId, CancellationToken.None));
        Assert.Null(await queries.GetAsync(seed.OrganizationId, assigned, seed.OtherOrganizationAuditId, CancellationToken.None));

        AuditViewerFilter exact = Filter() with { CorrelationId = "corr-branch-1", Action = "branchUpdated", Result = "succeeded" };
        AuditLogListResponse filtered = await queries.ListAsync(seed.OrganizationId, assigned, exact, CancellationToken.None);
        Assert.Single(filtered.Items);
        Assert.Equal("corr-branch-1", filtered.Items[0].CorrelationId);

        AuditViewerOptionsResponse options = await queries.OptionsAsync(seed.OrganizationId, assigned, CancellationToken.None);
        Assert.Single(options.Branches);
        Assert.Equal(seed.Branch1, options.Branches[0].Id);
        string optionJson = JsonSerializer.Serialize(options);
        Assert.DoesNotContain(seed.OrganizationId.ToString("D"), optionJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("permissionCodes", optionJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roleGrants", optionJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", optionJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeCount, await audit.AuditEntries.CountAsync());
        Assert.Empty(audit.ChangeTracker.Entries());
    }

    [Fact]
    public async Task DetailPresenter_AllowsOnlyActionSpecificSafeMetadataAndQueryBoundsAreEnforced()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Seed seed = await SeedAsync(database.ConnectionString);
        await using AuditDbContext audit = Audit(database.ConnectionString);
        await using IdentityDbContext identity = Identity(database.ConnectionString);
        await using OrganizationDbContext organization = Organization(database.ConnectionString);
        var queries = Queries(audit, identity, organization);
        EffectiveBranchAccessSnapshot all = Access("allOrganizationBranches", seed.Branch1, seed.Branch2);

        AuditLogDetailResponse detail = Assert.IsType<AuditLogDetailResponse>(
            await queries.GetAsync(seed.OrganizationId, all, seed.MetadataAuditId, CancellationToken.None));
        Assert.Equal("Management User", detail.ActorDisplayName);
        Assert.Equal(2, detail.Metadata?.ActiveDeviceCount);
        Assert.Equal(5, detail.Metadata?.ActiveUserAssignmentCount);
        string json = JsonSerializer.Serialize(detail);
        Assert.DoesNotContain("futureSecret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beforeJson", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("afterJson", json, StringComparison.OrdinalIgnoreCase);

        AuditViewerQueryException tooLarge = await Assert.ThrowsAsync<AuditViewerQueryException>(() => queries.ListAsync(
            seed.OrganizationId, all, Filter() with { FromUtc = Now.AddDays(-91) }, CancellationToken.None));
        Assert.Equal("audit.interval_too_large", tooLarge.Code);
        await queries.ListAsync(seed.OrganizationId, all, Filter(pageSize: 100), CancellationToken.None);
        AuditViewerQueryException pageSize = await Assert.ThrowsAsync<AuditViewerQueryException>(() => queries.ListAsync(
            seed.OrganizationId, all, Filter(pageSize: 101), CancellationToken.None));
        Assert.Equal("audit.invalid_filter", pageSize.Code);
    }

    private static AuditViewerFilter Filter(int pageSize = 25) => new(
        Now.AddDays(-7), Now.AddMinutes(1), null, null, null, null, null, null, null, null, pageSize);

    private static EffectiveBranchAccessSnapshot Access(string scope, params Guid[] branchIds)
        => new(scope, scope == "assignedBranches" ? branchIds : [], branchIds);

    private static AuditViewerQueries Queries(AuditDbContext audit, IdentityDbContext identity, OrganizationDbContext organization)
        => new(audit, identity, organization, new AuditViewerCursorCodec(new EphemeralDataProtectionProvider()));

    private static async Task<Seed> SeedAsync(string connectionString)
    {
        await using (OrganizationDbContext context = Organization(connectionString)) await context.Database.MigrateAsync();
        await using (IdentityDbContext context = Identity(connectionString)) await context.Database.MigrateAsync();
        await using (AuditDbContext context = Audit(connectionString)) await context.Database.MigrateAsync();

        Guid organizationId = Guid.NewGuid();
        Guid branch1 = Guid.NewGuid();
        Guid branch2 = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        await using (OrganizationDbContext context = Organization(connectionString))
        {
            OrganizationAggregate tenant = OrganizationAggregate.Create(organizationId, new OrganizationName("Kalm", 120), null, new CurrencyCode("EGP"), new LocaleCode("en"), Now);
            tenant.ChangeStatus(OrganizationStatus.Active, Now);
            Branch first = Branch.Create(branch1, organizationId, new OrganizationName("Cairo", 120), new BranchCode("CAI"), new LocaleCode("en"), new TimeZoneId("Africa/Cairo"), BusinessDayRollover.Parse("04:00"), Now);
            Branch second = Branch.Create(branch2, organizationId, new OrganizationName("Giza", 120), new BranchCode("GIZ"), new LocaleCode("en"), new TimeZoneId("Africa/Cairo"), BusinessDayRollover.Parse("04:00"), Now);
            first.Activate(Now); second.Activate(Now);
            context.AddRange(tenant, first, second);
            await context.SaveChangesAsync();
        }
        await using (IdentityDbContext context = Identity(connectionString))
        {
            context.Users.Add(User.Create(userId, organizationId, new Username("manager"), new EmailAddress("secret@example.com"), new DisplayName("Management User"), "en", Now));
            await context.SaveChangesAsync();
        }

        Guid branch2Audit = Guid.NewGuid();
        Guid branchlessAudit = Guid.NewGuid();
        Guid otherOrganizationAudit = Guid.NewGuid();
        Guid metadataAudit = Guid.NewGuid();
        await using (AuditDbContext context = Audit(connectionString))
        {
            var entries = new List<AuditEntry>();
            for (int index = 0; index < 5; index++)
            {
                entries.Add(Entry(Guid.NewGuid(), organizationId, branch1, userId, AuditAction.BranchUpdated, $"corr-branch-{index + 1}"));
            }
            entries.Add(Entry(branch2Audit, organizationId, branch2, userId, AuditAction.DeviceUpdated, "corr-branch-2"));
            entries.Add(Entry(branchlessAudit, organizationId, null, userId, AuditAction.RoleCreated, "corr-branchless"));
            entries.Add(Entry(otherOrganizationAudit, Guid.NewGuid(), branch1, null, AuditAction.BranchUpdated, "corr-other"));
            entries.Add(AuditEntry.Create(metadataAudit, Now, organizationId, branch1, null, userId, AuditActorType.User, null,
                AuditAction.BranchAdministrationRejected, "Branch", branch1, AuditResult.Denied, "branch.dependencies_active", "corr-metadata",
                "{\"status\":\"active\",\"email\":\"private@example.com\"}",
                "{\"dependencyCounts\":{\"registeredDeviceCount\":1,\"activeDeviceCount\":2,\"activeCredentialCount\":3,\"activeSessionCount\":4,\"activeUserAssignmentCount\":5},\"password\":\"never\",\"futureSecret\":\"omit\"}", null, null));
            context.AuditEntries.AddRange(entries);
            await context.SaveChangesAsync();
        }
        return new Seed(organizationId, branch1, branch2, branch2Audit, branchlessAudit, otherOrganizationAudit, metadataAudit);
    }

    private static AuditEntry Entry(Guid id, Guid organizationId, Guid? branchId, Guid? actorId, AuditAction action, string correlation)
        => AuditEntry.Create(id, Now, organizationId, branchId, null, actorId, actorId is null ? AuditActorType.System : AuditActorType.User,
            null, action, "Branch", branchId, AuditResult.Succeeded, null, correlation, null, null, null, null);

    private static OrganizationDbContext Organization(string connectionString) => new(new DbContextOptionsBuilder<OrganizationDbContext>()
        .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "organization")).Options);
    private static IdentityDbContext Identity(string connectionString) => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "identity")).Options);
    private static AuditDbContext Audit(string connectionString) => new(new DbContextOptionsBuilder<AuditDbContext>()
        .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "audit")).Options);

    private sealed record Seed(Guid OrganizationId, Guid Branch1, Guid Branch2, Guid Branch2AuditId, Guid BranchlessAuditId, Guid OtherOrganizationAuditId, Guid MetadataAuditId);

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
            string name = $"kalm_audit_viewer_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await new NpgsqlCommand($"create database \"{name}\"", connection).ExecuteNonQueryAsync();
            return new TestDatabase(admin, new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString, name);
        }
        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_admin) { Pooling = false }.ConnectionString);
            await connection.OpenAsync();
            await new NpgsqlCommand($"drop database if exists \"{_name}\" with (force)", connection).ExecuteNonQueryAsync();
        }
    }
}
