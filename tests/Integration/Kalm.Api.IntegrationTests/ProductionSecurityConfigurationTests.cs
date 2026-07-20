using Kalm.Api.Configuration;
using Kalm.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kalm.Api.IntegrationTests;

public sealed class ProductionSecurityConfigurationTests
{
    [Fact]
    public async Task ProductionStartup_FailsWhenFingerprintKeyIsAbsent()
    {
        WebApplicationBuilder builder = CreateProductionBuilder(new Dictionary<string, string?>
        {
            ["PasswordHashing:Iterations"] = PasswordHashingOptions.MinimumIterations.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        builder.Services.AddIdentityInfrastructure(builder.Configuration, _ => "unused");
        await using WebApplication app = builder.Build();

        OptionsValidationException failure = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync());

        Assert.Contains("fingerprint key", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionStartup_FailsWhenPersistentDataProtectionPathIsAbsent()
    {
        WebApplicationBuilder builder = CreateProductionBuilder(new Dictionary<string, string?>());

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => builder.AddKalmDataProtection());

        Assert.Contains("persistent Data Protection key-ring path", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionStartup_FailsWhenDataProtectionAtRestProtectionIsAbsent()
    {
        string keyPath = Path.Combine(Path.GetTempPath(), "kalm-production-security-tests", Guid.NewGuid().ToString("N"));
        try
        {
            WebApplicationBuilder builder = CreateProductionBuilder(new Dictionary<string, string?>
            {
                ["DataProtection:KeyRingPath"] = keyPath
            });

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => builder.AddKalmDataProtection());

            Assert.Contains("protected at rest", failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(keyPath))
            {
                Directory.Delete(keyPath, recursive: true);
            }
        }
    }

    private static WebApplicationBuilder CreateProductionBuilder(IReadOnlyDictionary<string, string?> values)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(values);
        builder.Logging.ClearProviders();
        return builder;
    }
}
