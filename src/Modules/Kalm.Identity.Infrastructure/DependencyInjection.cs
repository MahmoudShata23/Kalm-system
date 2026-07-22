using Kalm.Identity.Application.ManagementAuthentication;
using Kalm.Identity.Application.PinAuthentication;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration, Func<IServiceProvider, string> connectionString)
    {
        services.AddOptions<PasswordHashingOptions>()
            .Configure(options => options.Iterations = ReadInt(configuration, $"{PasswordHashingOptions.SectionName}:Iterations", options.Iterations))
            .Validate(options => options.Iterations >= PasswordHashingOptions.MinimumIterations, "Password work factor must be at least 220000 iterations.")
            .ValidateOnStart();
        services.AddOptions<SecurityFingerprintOptions>()
            .Configure(options =>
            {
                options.ActiveKeyVersion = ReadInt(configuration, $"{SecurityFingerprintOptions.SectionName}:ActiveKeyVersion", options.ActiveKeyVersion);
                options.ActiveKeyBase64 = configuration[$"{SecurityFingerprintOptions.SectionName}:ActiveKeyBase64"] ?? string.Empty;
            })
            .Validate(options => options.ActiveKeyVersion > 0 && !string.IsNullOrWhiteSpace(options.ActiveKeyBase64), "A versioned fingerprint key is required.")
            .ValidateOnStart();
        services.AddDbContext<IdentityDbContext>((provider, options) =>
            options.UseNpgsql(connectionString(provider), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity")));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPinHasher, Pbkdf2PinHasher>();
        services.AddSingleton<ISecurityFingerprintProvider, HmacSecurityFingerprintProvider>();
        return services;
    }

    private static int ReadInt(IConfiguration configuration, string key, int fallback)
        => int.TryParse(configuration[key], out int value) ? value : fallback;
}
