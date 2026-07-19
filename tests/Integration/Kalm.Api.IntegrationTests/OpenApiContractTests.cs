using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kalm.Api.IntegrationTests;

public sealed class OpenApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string UpdateEnvironmentVariable = "KALM_UPDATE_OPENAPI";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApiDocument_MatchesCommittedSnapshot()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);

        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(CancellationToken.None);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: CancellationToken.None);

        string snapshot = NormalizeLineEndings(JsonSerializer.Serialize(document.RootElement, SerializerOptions)) + "\n";
        string snapshotPath = Path.Combine(FindRepositoryRoot(), "contracts", "openapi", "kalm-api.v1.json");

        if (string.Equals(Environment.GetEnvironmentVariable(UpdateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, snapshot, new UTF8Encoding(false), CancellationToken.None);
            return;
        }

        Assert.True(File.Exists(snapshotPath), $"OpenAPI snapshot is missing: {snapshotPath}");

        string committedSnapshot = NormalizeLineEndings(
            await File.ReadAllTextAsync(snapshotPath, CancellationToken.None));

        Assert.Equal(committedSnapshot, snapshot);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kalm.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root containing Kalm.slnx.");
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
