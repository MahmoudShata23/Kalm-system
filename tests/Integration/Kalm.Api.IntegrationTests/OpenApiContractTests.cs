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

    [Fact]
    public async Task BranchAdministration_ExposesExactlyTheApprovedSixRoutes()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(CancellationToken.None);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: CancellationToken.None);
        string[] paths = document.RootElement.GetProperty("paths").EnumerateObject()
            .Select(path => path.Name)
            .Where(path => path.StartsWith("/api/v1/management/branches", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "/api/v1/management/branches",
                "/api/v1/management/branches/{branchId}",
                "/api/v1/management/branches/{branchId}/activate",
                "/api/v1/management/branches/{branchId}/deactivate"
            ],
            paths);
        JsonElement branches = document.RootElement.GetProperty("paths").GetProperty("/api/v1/management/branches");
        Assert.True(branches.TryGetProperty("get", out _));
        Assert.True(branches.TryGetProperty("post", out _));
        JsonElement detail = document.RootElement.GetProperty("paths").GetProperty("/api/v1/management/branches/{branchId}");
        Assert.True(detail.TryGetProperty("get", out _));
        Assert.True(detail.TryGetProperty("put", out _));
    }

    [Fact]
    public async Task AuditViewer_ExposesExactlyTheApprovedThreeReadOnlyEndpoints()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(CancellationToken.None);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: CancellationToken.None);
        JsonProperty[] paths = document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(path => path.Name.StartsWith("/api/v1/management/audit-logs", StringComparison.Ordinal))
            .OrderBy(path => path.Name, StringComparer.Ordinal).ToArray();
        Assert.Equal(
            ["/api/v1/management/audit-logs", "/api/v1/management/audit-logs/options", "/api/v1/management/audit-logs/{auditLogId}"],
            paths.Select(path => path.Name));
        Assert.All(paths, path => Assert.Equal(["get"], path.Value.EnumerateObject().Select(operation => operation.Name).ToArray()));
    }

    [Fact]
    public async Task CatalogAdministration_ExposesOnlyTheApprovedAggregateEndpointSet()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(CancellationToken.None);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: CancellationToken.None);
        JsonElement paths = document.RootElement.GetProperty("paths");
        JsonProperty[] catalog = paths.EnumerateObject()
            .Where(path => path.Name.StartsWith("/api/v1/management/catalog/", StringComparison.Ordinal))
            .OrderBy(path => path.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                "/api/v1/management/catalog/categories",
                "/api/v1/management/catalog/categories/order",
                "/api/v1/management/catalog/categories/{categoryId}",
                "/api/v1/management/catalog/categories/{categoryId}/activate",
                "/api/v1/management/catalog/categories/{categoryId}/archive",
                "/api/v1/management/catalog/products",
                "/api/v1/management/catalog/products/options",
                "/api/v1/management/catalog/products/{productId}",
                "/api/v1/management/catalog/products/{productId}/activate",
                "/api/v1/management/catalog/products/{productId}/archive"
            ],
            catalog.Select(path => path.Name));
        Assert.Equal(["get", "post"], paths.GetProperty("/api/v1/management/catalog/categories").EnumerateObject().Select(value => value.Name));
        Assert.Equal(["put"], paths.GetProperty("/api/v1/management/catalog/categories/order").EnumerateObject().Select(value => value.Name));
        Assert.Equal(["get", "put"], paths.GetProperty("/api/v1/management/catalog/categories/{categoryId}").EnumerateObject().Select(value => value.Name));
        Assert.Equal(["get", "post"], paths.GetProperty("/api/v1/management/catalog/products").EnumerateObject().Select(value => value.Name));
        Assert.Equal(["get", "put"], paths.GetProperty("/api/v1/management/catalog/products/{productId}").EnumerateObject().Select(value => value.Name));
        Assert.DoesNotContain(catalog, path => path.Name.Contains("/variants", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.SelectMany(path => path.Value.EnumerateObject()), operation => operation.Name == "delete");

        foreach (string mutationPath in catalog.Select(path => path.Name).Where(path =>
                     path.EndsWith("/order", StringComparison.Ordinal)
                     || path.EndsWith("/activate", StringComparison.Ordinal)
                     || path.EndsWith("/archive", StringComparison.Ordinal)))
        {
            JsonElement operation = paths.GetProperty(mutationPath).EnumerateObject().Single().Value;
            Assert.True(operation.GetProperty("responses").TryGetProperty("429", out _));
        }
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
