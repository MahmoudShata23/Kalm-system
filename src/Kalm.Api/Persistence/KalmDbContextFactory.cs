using Kalm.Api.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Kalm.Api.Persistence;

public sealed class KalmDbContextFactory : IDesignTimeDbContextFactory<KalmDbContext>
{
    private const string DevelopmentConnectionString =
        "Host=localhost;Port=54329;Database=kalm;Username=kalm;Password=kalm_dev_password";

    public KalmDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("KALM_DATABASE_CONNECTION_STRING") ?? DevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<KalmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new KalmDbContext(options);
    }
}

public static class KalmDatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddKalmDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Database connection string is required.")
            .ValidateOnStart();

        services.AddDbContext<KalmDbContext>((provider, options) =>
        {
            var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(databaseOptions.ConnectionString);
        });

        return services;
    }
}
