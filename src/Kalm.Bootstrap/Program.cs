using System.Data;
using Kalm.Audit.Application;
using Kalm.Audit.Domain;
using Kalm.Audit.Infrastructure.Persistence;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Identity.Infrastructure.Security;
using Kalm.Organization.Domain;
using Kalm.Organization.Domain.ValueObjects;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OrganizationAggregate = Kalm.Organization.Domain.Organization;

namespace Kalm.Bootstrap;

internal static class Program
{
    public static Task<int> Main(string[] args) => BootstrapProgram.RunAsync(args);
}

internal static class BootstrapProgram
{
    private const int Success = 0;
    private const int OperationFailed = 1;
    private const int InvalidUsage = 2;
    private const int AlreadyInitialized = 3;
    private const int MigrationsMissing = 4;
    private const int ConfigurationInvalid = 5;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "bootstrap-management", StringComparison.Ordinal))
        {
            WriteUsage();
            return InvalidUsage;
        }

        try
        {
            BootstrapArguments parsed = BootstrapArguments.Parse(args[1..]);
            char[] passwordBuffer = parsed.PasswordFromStandardInput ? ReadPasswordFromStandardInput() : ReadPasswordInteractively();
            try
            {
                string password = new(passwordBuffer);
                return await ExecuteAsync(parsed, password, CancellationToken.None);
            }
            finally
            {
                Array.Clear(passwordBuffer);
            }
        }
        catch (BootstrapAlreadyInitializedException)
        {
            Console.Error.WriteLine("Bootstrap refused: an Identity user already exists. No changes were made.");
            return AlreadyInitialized;
        }
        catch (PendingMigrationException)
        {
            Console.Error.WriteLine("Bootstrap refused: required database migrations have not been applied. No changes were made.");
            return MigrationsMissing;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Bootstrap input is invalid: {exception.Message}");
            return InvalidUsage;
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("Configuration:", StringComparison.Ordinal))
        {
            Console.Error.WriteLine(exception.Message);
            return ConfigurationInvalid;
        }
        catch (FileNotFoundException exception)
        {
            Console.Error.WriteLine($"Bootstrap failed because required runtime file '{Path.GetFileName(exception.FileName)}' was unavailable. No credential or secret details were written to output.");
            return OperationFailed;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Bootstrap failed. No credential or secret details were written to output.");
            return OperationFailed;
        }
    }

    internal static async Task<int> ExecuteAsync(BootstrapArguments args, string password, CancellationToken cancellationToken)
    {
        string connectionString = RequiredEnvironment("KALM_DATABASE_CONNECTION_STRING");
        int iterations = ParsePositiveEnvironment("KALM_PASSWORD_HASH_ITERATIONS");
        int fingerprintKeyVersion = ParsePositiveEnvironment("KALM_FINGERPRINT_KEY_VERSION");
        string fingerprintKey = RequiredEnvironment("KALM_FINGERPRINT_KEY_BASE64");
        var hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions { Iterations = iterations }));
        _ = new HmacSecurityFingerprintProvider(Options.Create(new SecurityFingerprintOptions
        {
            ActiveKeyVersion = fingerprintKeyVersion,
            ActiveKeyBase64 = fingerprintKey
        }));
        string encodedHash = hasher.Hash(password);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var organizationOptions = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "organization")).Options;
        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity")).Options;
        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit")).Options;
        await using var organization = new OrganizationDbContext(organizationOptions);
        await using var identity = new IdentityDbContext(identityOptions);
        await using var audit = new AuditDbContext(auditOptions);
        await EnsureNoPendingMigrationsAsync(organization, identity, audit, cancellationToken);

        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await organization.Database.UseTransactionAsync(transaction, cancellationToken);
        await identity.Database.UseTransactionAsync(transaction, cancellationToken);
        await audit.Database.UseTransactionAsync(transaction, cancellationToken);

        try
        {
            await identity.Database.ExecuteSqlRawAsync("lock table identity.users in access exclusive mode", cancellationToken);
            if (await identity.Users.AnyAsync(cancellationToken))
            {
                throw new BootstrapAlreadyInitializedException();
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            OrganizationAggregate? organizationAggregate = await organization.Organizations.SingleOrDefaultAsync(cancellationToken);
            if (organizationAggregate is null)
            {
                organizationAggregate = OrganizationAggregate.Create(
                    Guid.NewGuid(), new OrganizationName(args.OrganizationName, 120), null,
                    new CurrencyCode(args.Currency), new LocaleCode(args.Locale), now);
                organization.Organizations.Add(organizationAggregate);
            }

            if (!await organization.Branches.AnyAsync(cancellationToken))
            {
                organization.Branches.Add(Branch.Create(
                    Guid.NewGuid(), organizationAggregate.Id, new OrganizationName(args.BranchName, 120),
                    new BranchCode(args.BranchCode), new LocaleCode(args.Locale), new TimeZoneId(args.TimeZone),
                    BusinessDayRollover.Parse(args.Rollover), now));
            }

            Guid userId = Guid.NewGuid();
            var user = User.Create(
                userId, organizationAggregate.Id, new Username(args.Username),
                string.IsNullOrWhiteSpace(args.Email) ? null : new EmailAddress(args.Email),
                new DisplayName(args.DisplayName), args.PreferredLanguage, now);
            PasswordCredential credential = PasswordCredential.Create(Guid.NewGuid(), userId, now);
            credential.CompleteSetup(encodedHash, now);
            user.Activate(credential, now);
            identity.Users.Add(user);
            identity.PasswordCredentials.Add(credential);

            var auditWriter = new AuditWriter(audit);
            await auditWriter.AppendAsync(new AuditWriteRequest(
                Guid.NewGuid(), now, organizationAggregate.Id, null, null, userId, AuditActorType.System, null,
                AuditAction.PasswordCredentialActivated, "User", userId, AuditResult.Succeeded, null,
                Guid.NewGuid().ToString("N"), null, null, null, null), cancellationToken);
            await auditWriter.AppendAsync(new AuditWriteRequest(
                Guid.NewGuid(), now, organizationAggregate.Id, null, null, userId, AuditActorType.System, null,
                AuditAction.OperationalBootstrapCompleted, "User", userId, AuditResult.Succeeded, null,
                Guid.NewGuid().ToString("N"), null, null, null, null), cancellationToken);

            await organization.SaveChangesAsync(cancellationToken);
            await identity.SaveChangesAsync(cancellationToken);
            await audit.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            Console.WriteLine("Initial management user created successfully.");
            return Success;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task EnsureNoPendingMigrationsAsync(
        OrganizationDbContext organization,
        IdentityDbContext identity,
        AuditDbContext audit,
        CancellationToken cancellationToken)
    {
        if ((await organization.Database.GetPendingMigrationsAsync(cancellationToken)).Any()
            || (await identity.Database.GetPendingMigrationsAsync(cancellationToken)).Any()
            || (await audit.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            throw new PendingMigrationException();
        }
    }

    private static char[] ReadPasswordFromStandardInput()
    {
        if (!Console.IsInputRedirected)
        {
            throw new ArgumentException("--password-stdin requires redirected standard input.");
        }

        return (Console.ReadLine() ?? string.Empty).ToCharArray();
    }

    private static char[] ReadPasswordInteractively()
    {
        if (Console.IsInputRedirected)
        {
            throw new ArgumentException("Use --password-stdin when standard input is redirected.");
        }

        Console.Error.Write("Password: ");
        var password = new List<char>();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return [.. password];
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Count > 0)
                {
                    password.RemoveAt(password.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Add(key.KeyChar);
            }
        }
    }

    private static string RequiredEnvironment(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Configuration: {name} is required.");

    private static int ParsePositiveEnvironment(string name)
        => int.TryParse(RequiredEnvironment(name), out int value) && value > 0
            ? value
            : throw new InvalidOperationException($"Configuration: {name} must be a positive integer.");

    private static void WriteUsage()
        => Console.Error.WriteLine(
            "Usage: bootstrap-management --username VALUE --display-name VALUE --organization-name VALUE --branch-name VALUE --branch-code VALUE --currency VALUE --locale VALUE --time-zone VALUE --rollover HH:mm --preferred-language en|ar [--email VALUE] [--password-stdin]");

    private sealed class BootstrapAlreadyInitializedException : Exception;
    private sealed class PendingMigrationException : Exception;
}

internal sealed record BootstrapArguments(
    string Username,
    string DisplayName,
    string? Email,
    string PreferredLanguage,
    string OrganizationName,
    string BranchName,
    string BranchCode,
    string Currency,
    string Locale,
    string TimeZone,
    string Rollover,
    bool PasswordFromStandardInput)
{
    public static BootstrapArguments Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool passwordStdin = false;
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (option == "--password-stdin")
            {
                passwordStdin = true;
                continue;
            }

            if (!option.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '{option}' is invalid.");
            }

            if (!values.TryAdd(option, args[++index]))
            {
                throw new ArgumentException($"Option '{option}' was supplied more than once.");
            }
        }

        string Required(string option) => values.Remove(option, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"{option} is required.");
        string? email = values.Remove("--email", out string? emailValue) ? emailValue : null;
        var result = new BootstrapArguments(
            Required("--username"), Required("--display-name"), email, Required("--preferred-language"),
            Required("--organization-name"), Required("--branch-name"), Required("--branch-code"),
            Required("--currency"), Required("--locale"), Required("--time-zone"), Required("--rollover"), passwordStdin);
        if (values.Count > 0)
        {
            throw new ArgumentException($"Unknown option '{values.Keys.First()}'.");
        }

        return result;
    }
}
