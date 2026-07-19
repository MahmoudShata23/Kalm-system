using System.Net;
using System.Net.Http.Json;
using Kalm.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Kalm.Api.IntegrationTests;

public sealed class PostgreSqlFoundationTests
{
    private const string InitialMigrationName = "20260715140000_InitialFoundation";

    [Fact]
    public async Task InitialMigration_AppliesToCleanPostgreSqlDatabase()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();

        var options = new DbContextOptionsBuilder<KalmDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;

        await using var context = new KalmDbContext(options);

        await context.Database.MigrateAsync(CancellationToken.None);

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(CancellationToken.None);
        Assert.Contains(InitialMigrationName, appliedMigrations);

        Assert.Equal("platform.outbox_messages", await ResolveTableName(database.ConnectionString, "platform.outbox_messages"));
        Assert.Equal("platform.idempotency_records", await ResolveTableName(database.ConnectionString, "platform.idempotency_records"));
        Assert.Equal("platform.schema_markers", await ResolveTableName(database.ConnectionString, "platform.schema_markers"));
        await AssertPlatformConstraints(database.ConnectionString);
    }

    [Fact]
    public async Task PreviouslyReleasedDatabase_UpgradesWithoutRecreatingFoundationTables()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();

        var options = new DbContextOptionsBuilder<KalmDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;

        await using var context = new KalmDbContext(options);

        await context.Database.MigrateAsync(InitialMigrationName, CancellationToken.None);

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(CancellationToken.None);
        Assert.Equal([InitialMigrationName], appliedMigrations);

        var marker = new SchemaMarker(Guid.NewGuid(), "upgrade-sentinel", DateTimeOffset.UtcNow);
        context.SchemaMarkers.Add(marker);
        await context.SaveChangesAsync(CancellationToken.None);

        await context.Database.MigrateAsync(CancellationToken.None);

        var persistedMarker = await context.SchemaMarkers
            .SingleAsync(schemaMarker => schemaMarker.Id == marker.Id, CancellationToken.None);

        Assert.Equal(marker.Name, persistedMarker.Name);
        await AssertPlatformConstraints(database.ConnectionString);
    }

    [Fact]
    public async Task ReadinessEndpoint_VerifiesPostgreSqlConnectivity()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();

        var options = new DbContextOptionsBuilder<KalmDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;

        await using (var context = new KalmDbContext(options))
        {
            await context.Database.MigrateAsync(CancellationToken.None);
        }

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:ConnectionString"] = database.ConnectionString
                    });
                });
            });

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>(CancellationToken.None);
        Assert.Equal("Healthy", payload?.Status);
        Assert.Equal("Kalm.Api", payload?.Service);
    }

    private static async Task<string?> ResolveTableName(string connectionString, string tableName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);

        await using var command = connection.CreateCommand();
        command.CommandText = "select to_regclass(@tableName)::text;";
        command.Parameters.AddWithValue("tableName", tableName);

        return (string?)await command.ExecuteScalarAsync(CancellationToken.None);
    }

    private static async Task AssertPlatformConstraints(string connectionString)
    {
        Assert.Equal("pk_idempotency_records", await ResolvePrimaryKeyName(connectionString, "idempotency_records"));
        Assert.Equal("pk_outbox_messages", await ResolvePrimaryKeyName(connectionString, "outbox_messages"));
        Assert.Equal("pk_schema_markers", await ResolvePrimaryKeyName(connectionString, "schema_markers"));
        Assert.Equal("platform.ix_idempotency_records_key", await ResolveTableName(connectionString, "platform.ix_idempotency_records_key"));
        Assert.Equal("platform.ix_outbox_messages_processed_at_utc_occurred_at_utc", await ResolveTableName(connectionString, "platform.ix_outbox_messages_processed_at_utc_occurred_at_utc"));
        Assert.Equal("platform.ix_schema_markers_name", await ResolveTableName(connectionString, "platform.ix_schema_markers_name"));
    }

    private static async Task<string?> ResolvePrimaryKeyName(string connectionString, string tableName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select constraint_name
            from information_schema.table_constraints
            where table_schema = 'platform'
              and table_name = @tableName
              and constraint_type = 'PRIMARY KEY';
            """;
        command.Parameters.AddWithValue("tableName", tableName);

        return (string?)await command.ExecuteScalarAsync(CancellationToken.None);
    }

    private sealed record HealthPayload(string Status, string Service);

    private sealed class PostgreSqlTestDatabase : IAsyncDisposable
    {
        private const string DefaultAdminConnectionString =
            "Host=localhost;Port=54329;Database=postgres;Username=kalm;Password=kalm_dev_password";

        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private PostgreSqlTestDatabase(string adminConnectionString, string connectionString, string databaseName)
        {
            _adminConnectionString = adminConnectionString;
            ConnectionString = connectionString;
            _databaseName = databaseName;
        }

        public string ConnectionString { get; }

        public static async Task<PostgreSqlTestDatabase> CreateAsync()
        {
            var adminConnectionString =
                Environment.GetEnvironmentVariable("KALM_TEST_POSTGRES_ADMIN") ?? DefaultAdminConnectionString;
            var databaseName = $"kalm_integration_{Guid.NewGuid():N}";

            var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);

            await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await connection.OpenAsync(CancellationToken.None);

            await using var command = connection.CreateCommand();
            command.CommandText = $"""create database "{databaseName}";""";
            await command.ExecuteNonQueryAsync(CancellationToken.None);

            var testBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
            {
                Database = databaseName
            };

            return new PostgreSqlTestDatabase(adminBuilder.ConnectionString, testBuilder.ConnectionString, databaseName);
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();

            var adminBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
            {
                Pooling = false
            };

            await using var connection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await connection.OpenAsync(CancellationToken.None);

            await using var command = connection.CreateCommand();
            command.CommandText = $"""drop database if exists "{_databaseName}" with (force);""";
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
