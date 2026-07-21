using System.Diagnostics;
using Kalm.Api.Persistence;
using Kalm.Bootstrap;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Authorization;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Identity.Infrastructure.Security;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kalm.Api.IntegrationTests;

[Collection("Bootstrap serial environment")]
public sealed class BootstrapTests
{
    private const string Password = "a secure bootstrap phrase ☕";

    [Fact]
    public async Task ConcurrentBootstrap_CreatesExactlyOneAtomicInitialUser()
    {
        await using var database = await BootstrapDatabase.CreateAsync();
        await database.MigrateAsync();
        using var environment = new BootstrapEnvironment(database.ConnectionString);
        BootstrapArguments args = CreateArguments();

        Task<int> first = BootstrapProgram.ExecuteAsync(args, Password, CancellationToken.None);
        Task<int> second = BootstrapProgram.ExecuteAsync(args, Password, CancellationToken.None);
        await Task.WhenAll(IgnoreFailureAsync(first), IgnoreFailureAsync(second));

        Assert.Equal(1, new[] { first, second }.Count(task => task.Status == TaskStatus.RanToCompletion && task.Result == 0));
        Assert.Equal(1, new[] { first, second }.Count(task => task.IsFaulted));
        await using var identity = database.CreateIdentity();
        Assert.Equal(1, await identity.Users.CountAsync());
        Assert.Equal(UserStatus.Active, (await identity.Users.SingleAsync()).Status);
        Assert.Equal(PasswordCredentialStatus.Active, (await identity.PasswordCredentials.SingleAsync()).Status);
        Assert.Equal(1, await identity.Roles.CountAsync());
        Assert.Equal(58, await identity.RolePermissions.CountAsync());
        Assert.Equal(1, await identity.UserRoleAssignments.CountAsync());
        Assert.Equal(2, (await identity.Users.SingleAsync()).AuthorizationVersion);
        Assert.Equal(
            PermissionCatalogue.FirstAdministratorPermissionCodes,
            await (from grant in identity.RolePermissions
                   join permission in identity.Permissions on grant.PermissionId equals permission.Id
                   orderby permission.Code
                   select permission.Code).ToArrayAsync());
        await using var organization = database.CreateOrganization();
        Assert.Equal(1, await organization.Organizations.CountAsync());
        Assert.Equal(1, await organization.Branches.CountAsync());
        Assert.Equal(1, await organization.UserBranchAccesses.CountAsync());
        Assert.Equal(1, await organization.UserBranchAssignments.CountAsync());
        Assert.Equal(BranchAccessScope.AssignedBranches, (await organization.UserBranchAccesses.SingleAsync()).Scope);
        await using var audit = database.CreateAudit();
        AuditAction[] actions = await audit.AuditEntries.OrderBy(entry => entry.Action).Select(entry => entry.Action).ToArrayAsync();
        AuditAction[] expectedActions =
        [
            AuditAction.AuthorizationProvisioningCompleted,
            AuditAction.OperationalBootstrapCompleted,
            AuditAction.PasswordCredentialActivated,
            AuditAction.RolePermissionSetChanged,
            AuditAction.SystemRoleProvisioned,
            AuditAction.UserBranchAccessChanged,
            AuditAction.UserRoleAssigned
        ];
        Assert.Equal(expectedActions.OrderBy(action => action), actions.OrderBy(action => action));
    }

    [Fact]
    public async Task CleanBootstrap_UsesAllOrganizationBranchesOnlyWhenExplicitlySelected()
    {
        await using var database = await BootstrapDatabase.CreateAsync();
        await database.MigrateAsync();
        using var environment = new BootstrapEnvironment(database.ConnectionString);
        BootstrapArguments args = BootstrapArguments.Parse([
            "--username", "manager", "--display-name", "Management User", "--preferred-language", "en",
            "--organization-name", "Kalm", "--branch-name", "Cairo", "--branch-code", "CAI-01",
            "--currency", "EGP", "--locale", "en", "--time-zone", "Africa/Cairo", "--rollover", "04:00",
            "--password-stdin", "--all-organization-branches"
        ]);

        Assert.Equal(0, await BootstrapProgram.ExecuteAsync(args, Password, CancellationToken.None));

        await using var organization = database.CreateOrganization();
        Assert.Equal(BranchAccessScope.AllOrganizationBranches, (await organization.UserBranchAccesses.SingleAsync()).Scope);
        Assert.Empty(await organization.UserBranchAssignments.ToListAsync());
    }

    [Fact]
    public async Task AuditFailure_RollsBackOrganizationIdentityAndAudit()
    {
        await using var database = await BootstrapDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.ExecuteAsync("create function audit.reject_bootstrap() returns trigger language plpgsql as $$ begin raise exception 'forced'; end; $$; create trigger trg_reject_bootstrap before insert on audit.audit_logs for each row execute function audit.reject_bootstrap();");
        using var environment = new BootstrapEnvironment(database.ConnectionString);

        await Assert.ThrowsAnyAsync<Exception>(() => BootstrapProgram.ExecuteAsync(CreateArguments(), Password, CancellationToken.None));

        await using var identity = database.CreateIdentity();
        Assert.Empty(await identity.Users.ToListAsync());
        await using var organization = database.CreateOrganization();
        Assert.Empty(await organization.Organizations.ToListAsync());
        Assert.Empty(await organization.Branches.ToListAsync());
        await using var audit = database.CreateAudit();
        Assert.Empty(await audit.AuditEntries.ToListAsync());
    }

    [Fact]
    public void CliArguments_RejectPasswordOptionAndUnknownOptions()
    {
        Assert.Throws<ArgumentException>(() => BootstrapArguments.Parse(["--password", "not-allowed"]));
        Assert.Throws<ArgumentException>(() => BootstrapArguments.Parse(["--unknown", "value"]));
    }

    [Fact]
    public async Task RealCli_ReadsPasswordFromStandardInputAndReturnsDocumentedSuccessAndAlreadyInitializedCodes()
    {
        await using var database = await BootstrapDatabase.CreateAsync();
        await database.MigrateAsync();

        CliResult first = await RunCliAsync(database.ConnectionString, ValidCliArguments(), Password);
        CliResult repeated = await RunCliAsync(database.ConnectionString, ValidCliArguments(), Password);

        Assert.True(first.ExitCode == 0, Describe(first));
        Assert.Contains("created successfully", first.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, first.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, first.StandardError, StringComparison.Ordinal);
        Assert.True(repeated.ExitCode == 3, Describe(repeated));
        Assert.Contains("already exists", repeated.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, repeated.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, repeated.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealCli_ReturnsMigrationsMissingCodeWithoutMutation()
    {
        await using var database = await BootstrapDatabase.CreateAsync();

        CliResult result = await RunCliAsync(database.ConnectionString, ValidCliArguments(), Password);

        Assert.True(result.ExitCode == 4, Describe(result));
        Assert.Contains("migrations have not been applied", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealCli_RejectsCommandLinePasswordWithInvalidUsageCode()
    {
        CliResult result = await RunCliAsync(
            "unused",
            ["bootstrap-management", "--password", Password],
            standardInput: null);

        Assert.True(result.ExitCode == 2, Describe(result));
        Assert.DoesNotContain(Password, result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealCli_ReturnsOperationFailedCodeWhenAuditFailureRollsBackBootstrap()
    {
        await using var database = await BootstrapDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.ExecuteAsync("create function audit.reject_real_cli_bootstrap() returns trigger language plpgsql as $$ begin raise exception 'forced'; end; $$; create trigger trg_reject_real_cli_bootstrap before insert on audit.audit_logs for each row execute function audit.reject_real_cli_bootstrap();");

        CliResult result = await RunCliAsync(database.ConnectionString, ValidCliArguments(), Password);

        Assert.True(result.ExitCode == 1, Describe(result));
        Assert.Contains("Bootstrap failed", result.StandardError, StringComparison.Ordinal);
        await using var identity = database.CreateIdentity();
        Assert.Empty(await identity.Users.ToListAsync());
        await using var organization = database.CreateOrganization();
        Assert.Empty(await organization.Organizations.ToListAsync());
        Assert.Empty(await organization.Branches.ToListAsync());
    }

    [Fact]
    public async Task RealCli_ReturnsConfigurationInvalidCodeWhenFingerprintKeyIsAbsent()
    {
        CliResult result = await RunCliAsync(
            "unused",
            ValidCliArguments(),
            Password,
            includeFingerprintKey: false);

        Assert.True(result.ExitCode == 5, Describe(result));
        Assert.Contains("KALM_FINGERPRINT_KEY_BASE64 is required", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingUserProvisioning_IsConcurrentIdempotentAndConflictingScopeFailsClosed()
    {
        await using var database = await BootstrapDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.SeedExistingUserAsync();
        using var environment = new BootstrapEnvironment(database.ConnectionString);
        string[] assigned = [
            "provision-first-administrator", "--username", "manager", "--branch-code", "CAI-01"
        ];

        Task<int> first = BootstrapProgram.RunAsync(assigned);
        Task<int> second = BootstrapProgram.RunAsync(assigned);
        int[] results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal(0, result));
        await using (var identity = database.CreateIdentity())
        {
            Assert.Equal(1, await identity.Roles.CountAsync());
            Assert.Equal(58, await identity.RolePermissions.CountAsync());
            Assert.Equal(1, await identity.UserRoleAssignments.CountAsync());
            Assert.Equal(2, (await identity.Users.SingleAsync()).AuthorizationVersion);
        }

        await using (var organization = database.CreateOrganization())
        {
            Assert.Equal(1, await organization.UserBranchAccesses.CountAsync());
            Assert.Equal(1, await organization.UserBranchAssignments.CountAsync());
        }

        int conflict = await BootstrapProgram.RunAsync([
            "provision-first-administrator", "--username", "manager", "--all-organization-branches"
        ]);
        Assert.Equal(6, conflict);

        await using (var identity = database.CreateIdentity())
        {
            User manager = await identity.Users.SingleAsync();
            var hasher = new Pbkdf2PasswordHasher(Microsoft.Extensions.Options.Options.Create(
                new PasswordHashingOptions { Iterations = PasswordHashingOptions.MinimumIterations }));
            var other = User.Create(
                Guid.NewGuid(), manager.OrganizationId,
                new Kalm.Identity.Domain.ValueObjects.Username("other-manager"), null,
                new Kalm.Identity.Domain.ValueObjects.DisplayName("Other Manager"), "en",
                new DateTimeOffset(2026, 7, 21, 1, 0, 0, TimeSpan.Zero));
            var credential = PasswordCredential.Create(
                Guid.NewGuid(), other.Id, new DateTimeOffset(2026, 7, 21, 1, 0, 0, TimeSpan.Zero));
            credential.CompleteSetup(
                hasher.Hash(Password), new DateTimeOffset(2026, 7, 21, 1, 0, 0, TimeSpan.Zero));
            other.Activate(credential, new DateTimeOffset(2026, 7, 21, 1, 0, 0, TimeSpan.Zero));
            identity.Users.Add(other);
            identity.PasswordCredentials.Add(credential);
            await identity.SaveChangesAsync();
        }

        int targetConflict = await BootstrapProgram.RunAsync([
            "provision-first-administrator", "--username", "other-manager", "--branch-code", "CAI-01"
        ]);
        Assert.Equal(6, targetConflict);
        await using var audit = database.CreateAudit();
        Assert.Contains(
            await audit.AuditEntries.Select(entry => entry.Action).ToArrayAsync(),
            action => action == AuditAction.AuthorizationProvisioningFailed);
    }

    [Fact]
    public async Task ExistingUserProvisioning_AllOrganizationBranchesIsExplicitAndIdempotent()
    {
        await using var database = await BootstrapDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.SeedExistingUserAsync();
        using var environment = new BootstrapEnvironment(database.ConnectionString);
        string[] arguments = [
            "provision-first-administrator", "--username", "manager", "--all-organization-branches"
        ];

        Assert.Equal(0, await BootstrapProgram.RunAsync(arguments));
        Assert.Equal(0, await BootstrapProgram.RunAsync(arguments));

        await using var identity = database.CreateIdentity();
        Assert.Equal(1, await identity.Roles.CountAsync());
        Assert.Equal(58, await identity.RolePermissions.CountAsync());
        Assert.Equal(1, await identity.UserRoleAssignments.CountAsync());
        Assert.Equal(2, (await identity.Users.SingleAsync()).AuthorizationVersion);
        Assert.Equal(
            PermissionCatalogue.FirstAdministratorPermissionCodes,
            await (from grant in identity.RolePermissions
                   join permission in identity.Permissions on grant.PermissionId equals permission.Id
                   orderby permission.Code
                   select permission.Code).ToArrayAsync());
        await using var organization = database.CreateOrganization();
        Assert.Equal(BranchAccessScope.AllOrganizationBranches, (await organization.UserBranchAccesses.SingleAsync()).Scope);
        Assert.Empty(await organization.UserBranchAssignments.ToListAsync());
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("organization")]
    [InlineData("audit")]
    public async Task ExistingUserProvisioning_ModuleFailureRollsBackEveryContextAndAuthorizationVersion(string failingModule)
    {
        await using var database = await BootstrapDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.SeedExistingUserAsync();
        string triggerSql = failingModule switch
        {
            "identity" => "create function identity.reject_authorization_test() returns trigger language plpgsql as $$ begin raise exception 'forced'; end; $$; create trigger trg_reject_authorization_test before insert on identity.roles for each row execute function identity.reject_authorization_test();",
            "organization" => "create function organization.reject_authorization_test() returns trigger language plpgsql as $$ begin raise exception 'forced'; end; $$; create trigger trg_reject_authorization_test before insert on organization.user_branch_access for each row execute function organization.reject_authorization_test();",
            _ => "create function audit.reject_authorization_test() returns trigger language plpgsql as $$ begin raise exception 'forced'; end; $$; create trigger trg_reject_authorization_test before insert on audit.audit_logs for each row execute function audit.reject_authorization_test();"
        };
        await database.ExecuteAsync(triggerSql);
        using var environment = new BootstrapEnvironment(database.ConnectionString);

        int result = await BootstrapProgram.RunAsync([
            "provision-first-administrator", "--username", "manager", "--branch-code", "CAI-01"
        ]);

        Assert.Equal(1, result);
        await using var identity = database.CreateIdentity();
        Assert.Equal(1, (await identity.Users.SingleAsync()).AuthorizationVersion);
        Assert.Empty(await identity.Roles.ToListAsync());
        Assert.Empty(await identity.RolePermissions.ToListAsync());
        Assert.Empty(await identity.UserRoleAssignments.ToListAsync());
        await using var organization = database.CreateOrganization();
        Assert.Empty(await organization.UserBranchAccesses.ToListAsync());
        Assert.Empty(await organization.UserBranchAssignments.ToListAsync());
        await using var audit = database.CreateAudit();
        Assert.Empty(await audit.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task ProvisioningConflictReportsFailureAuditOnlyAfterRollbackAndSurfacesAuditFailure()
    {
        await using var database = await BootstrapDatabase.CreateAsync();
        await database.MigrateAsync();
        await database.SeedExistingUserAsync();
        using var environment = new BootstrapEnvironment(database.ConnectionString);
        Assert.Equal(0, await BootstrapProgram.RunAsync([
            "provision-first-administrator", "--username", "manager", "--branch-code", "CAI-01"
        ]));
        await database.ExecuteAsync(
            "create function audit.reject_failed_authorization_test() returns trigger language plpgsql as $$ begin if new.action = 'AuthorizationProvisioningFailed' then raise exception 'forced'; end if; return new; end; $$; create trigger trg_reject_failed_authorization_test before insert on audit.audit_logs for each row execute function audit.reject_failed_authorization_test();");

        int result = await BootstrapProgram.RunAsync([
            "provision-first-administrator", "--username", "manager", "--all-organization-branches"
        ]);

        Assert.Equal(1, result);
        await using var identity = database.CreateIdentity();
        Assert.Equal(2, (await identity.Users.SingleAsync()).AuthorizationVersion);
        Assert.Equal(1, await identity.Roles.CountAsync());
        Assert.Equal(58, await identity.RolePermissions.CountAsync());
        await using var organization = database.CreateOrganization();
        Assert.Equal(BranchAccessScope.AssignedBranches, (await organization.UserBranchAccesses.SingleAsync()).Scope);
        Assert.Equal(1, await organization.UserBranchAssignments.CountAsync());
        await using var audit = database.CreateAudit();
        Assert.DoesNotContain(
            await audit.AuditEntries.Select(entry => entry.Action).ToArrayAsync(),
            action => action == AuditAction.AuthorizationProvisioningFailed);
    }

    private static BootstrapArguments CreateArguments() => BootstrapArguments.Parse([
        "--username", "manager", "--display-name", "Management User", "--preferred-language", "en",
        "--organization-name", "Kalm", "--branch-name", "Cairo", "--branch-code", "CAI-01",
        "--currency", "EGP", "--locale", "en", "--time-zone", "Africa/Cairo", "--rollover", "04:00",
        "--password-stdin"
    ]);

    private static string[] ValidCliArguments() =>
    [
        "bootstrap-management", "--username", "manager", "--display-name", "Management User",
        "--preferred-language", "en", "--organization-name", "Kalm", "--branch-name", "Cairo",
        "--branch-code", "CAI-01", "--currency", "EGP", "--locale", "en",
        "--time-zone", "Africa/Cairo", "--rollover", "04:00", "--password-stdin"
    ];

    private static async Task<CliResult> RunCliAsync(
        string connectionString,
        string[] arguments,
        string? standardInput,
        bool includeFingerprintKey = true)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(ResolveBootstrapAssemblyPath());
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["KALM_DATABASE_CONNECTION_STRING"] = connectionString;
        startInfo.Environment["KALM_PASSWORD_HASH_ITERATIONS"] = PasswordHashingOptions.MinimumIterations.ToString(System.Globalization.CultureInfo.InvariantCulture);
        startInfo.Environment["KALM_FINGERPRINT_KEY_VERSION"] = "1";
        if (includeFingerprintKey)
        {
            startInfo.Environment["KALM_FINGERPRINT_KEY_BASE64"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        }
        else
        {
            startInfo.Environment.Remove("KALM_FINGERPRINT_KEY_BASE64");
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Bootstrap CLI.");
        if (standardInput is not null)
        {
            await process.StandardInput.WriteLineAsync(standardInput);
        }

        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliResult(process.ExitCode, await output, await error);
    }

    private static string Describe(CliResult result)
        => $"Exit code: {result.ExitCode}{Environment.NewLine}stdout: {result.StandardOutput}{Environment.NewLine}stderr: {result.StandardError}";

    private static string ResolveBootstrapAssemblyPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kalm.slnx")))
        {
            directory = directory.Parent;
        }

        string root = directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not resolve the test build configuration.");
        string assembly = Path.Combine(root, "src", "Kalm.Bootstrap", "bin", configuration, "net10.0", "Kalm.Bootstrap.dll");
        return File.Exists(assembly) ? assembly : throw new FileNotFoundException("The built Bootstrap CLI was not found.", assembly);
    }

    private static async Task IgnoreFailureAsync(Task task)
    {
        try { await task; } catch (Exception) { }
    }

    [CollectionDefinition("Bootstrap serial environment", DisableParallelization = true)]
    public sealed class BootstrapTestGroup;

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class BootstrapEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = [];

        public BootstrapEnvironment(string connectionString)
        {
            Set("KALM_DATABASE_CONNECTION_STRING", connectionString);
            Set("KALM_PASSWORD_HASH_ITERATIONS", "220000");
            Set("KALM_FINGERPRINT_KEY_VERSION", "1");
            Set("KALM_FINGERPRINT_KEY_BASE64", Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()));
        }

        public void Dispose()
        {
            foreach ((string name, string? value) in _previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        private void Set(string name, string value)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private sealed class BootstrapDatabase : IAsyncDisposable
    {
        private const string DefaultAdmin = "Host=localhost;Port=54329;Database=postgres;Username=kalm;Password=kalm_dev_password";
        private readonly string _admin;
        private readonly string _name;
        private BootstrapDatabase(string admin, string name, string connectionString) { _admin = admin; _name = name; ConnectionString = connectionString; }
        public string ConnectionString { get; }

        public static async Task<BootstrapDatabase> CreateAsync()
        {
            string admin = Environment.GetEnvironmentVariable("KALM_TEST_POSTGRES_ADMIN") ?? DefaultAdmin;
            string name = $"kalm_bootstrap_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"create database \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
            return new BootstrapDatabase(admin, name, new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString);
        }

        public IdentityDbContext CreateIdentity() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(ConnectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "identity")).Options);
        public OrganizationDbContext CreateOrganization() => new(new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(ConnectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "organization")).Options);
        public AuditDbContext CreateAudit() => new(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(ConnectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "audit")).Options);

        public async Task MigrateAsync()
        {
            await using var platform = new KalmDbContext(new DbContextOptionsBuilder<KalmDbContext>().UseNpgsql(ConnectionString).Options);
            await using var organization = CreateOrganization();
            await using var identity = CreateIdentity();
            await using var audit = CreateAudit();
            await platform.Database.MigrateAsync();
            await organization.Database.MigrateAsync();
            await identity.Database.MigrateAsync();
            await audit.Database.MigrateAsync();
        }

        public async Task SeedExistingUserAsync()
        {
            DateTimeOffset now = new(2026, 7, 21, 0, 0, 0, TimeSpan.Zero);
            await using var organization = CreateOrganization();
            var organizationAggregate = Kalm.Organization.Domain.Organization.Create(
                Guid.NewGuid(), new Kalm.Organization.Domain.ValueObjects.OrganizationName("Kalm", 120), null,
                new Kalm.Organization.Domain.ValueObjects.CurrencyCode("EGP"),
                new Kalm.Organization.Domain.ValueObjects.LocaleCode("en"), now);
            organization.Organizations.Add(organizationAggregate);
            organization.Branches.Add(Kalm.Organization.Domain.Branch.Create(
                Guid.NewGuid(), organizationAggregate.Id,
                new Kalm.Organization.Domain.ValueObjects.OrganizationName("Cairo", 120),
                new Kalm.Organization.Domain.ValueObjects.BranchCode("CAI-01"),
                new Kalm.Organization.Domain.ValueObjects.LocaleCode("en"),
                new Kalm.Organization.Domain.ValueObjects.TimeZoneId("Africa/Cairo"),
                Kalm.Organization.Domain.ValueObjects.BusinessDayRollover.Parse("04:00"), now));
            await organization.SaveChangesAsync();

            await using var identity = CreateIdentity();
            var hasher = new Pbkdf2PasswordHasher(Microsoft.Extensions.Options.Options.Create(
                new PasswordHashingOptions { Iterations = PasswordHashingOptions.MinimumIterations }));
            var user = User.Create(
                Guid.NewGuid(), organizationAggregate.Id, new Kalm.Identity.Domain.ValueObjects.Username("manager"), null,
                new Kalm.Identity.Domain.ValueObjects.DisplayName("Management User"), "en", now);
            var credential = PasswordCredential.Create(Guid.NewGuid(), user.Id, now);
            credential.CompleteSetup(hasher.Hash(Password), now);
            user.Activate(credential, now);
            identity.Users.Add(user);
            identity.PasswordCredentials.Add(credential);
            await identity.SaveChangesAsync();
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
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
