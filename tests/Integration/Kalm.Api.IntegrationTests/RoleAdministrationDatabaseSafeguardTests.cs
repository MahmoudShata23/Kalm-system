using Kalm.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kalm.Api.IntegrationTests;

public sealed class RoleAdministrationDatabaseSafeguardTests
{
    [Fact]
    public async Task DirectSql_RejectsActiveRoleWithoutPermissionAndAllowsCompletedReplacement()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateIdentityAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        Guid invalidRoleId = Guid.NewGuid();

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
            await InsertRoleAsync(connection, transaction, invalidRoleId, organizationId, "Invalid role");
            PostgresException failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal("ck_identity_active_role_has_permission", failure.ConstraintName);
        }

        Guid roleId = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
            await InsertRoleAsync(connection, transaction, roleId, organizationId, "Completed role");
            await InsertGrantAsync(connection, transaction, roleId, "reports.sales");
            await transaction.CommitAsync();
        }

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
            await using var revoke = new NpgsqlCommand(
                "update identity.role_permissions set revoked_at_utc = now(), version = version + 1 where role_id = @roleId and revoked_at_utc is null",
                connection,
                transaction);
            revoke.Parameters.AddWithValue("roleId", roleId);
            Assert.Equal(1, await revoke.ExecuteNonQueryAsync());
            await InsertGrantAsync(connection, transaction, roleId, "reports.inventory");
            await transaction.CommitAsync();
        }
    }

    [Fact]
    public async Task LastManagementAccess_ConcurrentSubtractionsAllowAtMostOneCommit()
    {
        await using var database = await TestDatabase.CreateAsync();
        await MigrateIdentityAsync(database.ConnectionString);
        Guid organizationId = Guid.NewGuid();
        Guid firstRoleId = Guid.NewGuid();
        Guid secondRoleId = Guid.NewGuid();
        Guid firstUserId = Guid.NewGuid();
        Guid secondUserId = Guid.NewGuid();
        await SeedManagementUserAsync(database.ConnectionString, organizationId, firstUserId, firstRoleId, "manager-one");
        await SeedManagementUserAsync(database.ConnectionString, organizationId, secondUserId, secondRoleId, "manager-two");

        await using var firstConnection = new NpgsqlConnection(database.ConnectionString);
        await using var secondConnection = new NpgsqlConnection(database.ConnectionString);
        await Task.WhenAll(firstConnection.OpenAsync(), secondConnection.OpenAsync());
        await using NpgsqlTransaction firstTransaction = await firstConnection.BeginTransactionAsync();
        await using NpgsqlTransaction secondTransaction = await secondConnection.BeginTransactionAsync();
        await SuspendUserAsync(firstConnection, firstTransaction, firstUserId);
        await SuspendUserAsync(secondConnection, secondTransaction, secondUserId);

        Task<bool> firstCommit = TryCommitAsync(firstTransaction);
        Task<bool> secondCommit = TryCommitAsync(secondTransaction);
        bool[] outcomes = await Task.WhenAll(firstCommit, secondCommit);

        Assert.Equal(1, outcomes.Count(succeeded => succeeded));
        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var count = new NpgsqlCommand("select identity.effective_management_user_count(@organizationId)", verify);
        count.Parameters.AddWithValue("organizationId", organizationId);
        Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
    }

    private static async Task<bool> TryCommitAsync(NpgsqlTransaction transaction)
    {
        try
        {
            await transaction.CommitAsync();
            return true;
        }
        catch (PostgresException exception)
        {
            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal("ck_identity_last_management_access", exception.ConstraintName);
            return false;
        }
    }

    private static async Task SeedManagementUserAsync(
        string connectionString,
        Guid organizationId,
        Guid userId,
        Guid roleId,
        string username)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await using (var user = new NpgsqlCommand(
            """
            insert into identity.users
              (id, organization_id, username, normalized_username, email, normalized_email, display_name, preferred_language,
               status, version, authorization_version, created_at_utc, updated_at_utc, activated_at_utc, archived_at_utc)
            values (@id, @organizationId, @username, @normalized, null, null, @username, 'en', 'Active', 1, 1, now(), now(), now(), null)
            """,
            connection,
            transaction))
        {
            user.Parameters.AddWithValue("id", userId);
            user.Parameters.AddWithValue("organizationId", organizationId);
            user.Parameters.AddWithValue("username", username);
            user.Parameters.AddWithValue("normalized", username.ToUpperInvariant());
            await user.ExecuteNonQueryAsync();
        }

        await InsertRoleAsync(connection, transaction, roleId, organizationId, username + " role");
        await InsertGrantAsync(connection, transaction, roleId, "management.access");
        await using (var assignment = new NpgsqlCommand(
            "insert into identity.user_role_assignments (id, organization_id, user_id, role_id, assigned_at_utc, revoked_at_utc, version) values (@id, @organizationId, @userId, @roleId, now(), null, 1)",
            connection,
            transaction))
        {
            assignment.Parameters.AddWithValue("id", Guid.NewGuid());
            assignment.Parameters.AddWithValue("organizationId", organizationId);
            assignment.Parameters.AddWithValue("userId", userId);
            assignment.Parameters.AddWithValue("roleId", roleId);
            await assignment.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task InsertRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid roleId,
        Guid organizationId,
        string name)
    {
        await using var command = new NpgsqlCommand(
            "insert into identity.roles (id, organization_id, name, normalized_name, system_key, status, version, created_at_utc, updated_at_utc, archived_at_utc) values (@id, @organizationId, @name, @normalized, null, 'Active', 1, now(), now(), null)",
            connection,
            transaction);
        command.Parameters.AddWithValue("id", roleId);
        command.Parameters.AddWithValue("organizationId", organizationId);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("normalized", name.ToUpperInvariant());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid roleId,
        string permissionCode)
    {
        await using var command = new NpgsqlCommand(
            "insert into identity.role_permissions (id, role_id, permission_id, granted_at_utc, revoked_at_utc, version) select @id, @roleId, id, now(), null, 1 from identity.permissions where code = @code",
            connection,
            transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("code", permissionCode);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task SuspendUserAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId)
    {
        await using var command = new NpgsqlCommand(
            "update identity.users set status = 'Suspended', version = version + 1, updated_at_utc = now() where id = @userId and status = 'Active'",
            connection,
            transaction);
        command.Parameters.AddWithValue("userId", userId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task MigrateIdentityAsync(string connectionString)
    {
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__ef_migrations_history", "identity"))
            .Options);
        await context.Database.MigrateAsync();
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
            string name = $"kalm_roles_{Guid.NewGuid():N}";
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
