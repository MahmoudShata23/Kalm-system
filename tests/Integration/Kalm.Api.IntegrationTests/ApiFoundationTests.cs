using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kalm.Api.Infrastructure.Correlation;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kalm.Api.IntegrationTests;

public sealed class ApiFoundationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiFoundationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsHealthyStatusAndCorrelationHeader()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "test-correlation-id");

        using var response = await client.GetAsync("/health/live", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.Contains("test-correlation-id", values);

        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>(CancellationToken.None);
        Assert.Equal("Healthy", payload?.Status);
        Assert.Equal("Kalm.Api", payload?.Service);
    }

    [Fact]
    public async Task AuthMeEndpoint_ReturnsAnonymousSkeletonState()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/auth/me", CancellationToken.None);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserPayload>(CancellationToken.None);

        Assert.NotNull(payload);
        Assert.False(payload.IsAuthenticated);
        Assert.Empty(payload.Permissions);
    }

    [Fact]
    public async Task LoginEndpoint_ReturnsStableProblemCodeUntilMilestoneOne()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = "demo", secret = "12345" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);

        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(CancellationToken.None),
            cancellationToken: CancellationToken.None);

        Assert.Equal("iam.not_configured", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }

    [Theory]
    [InlineData("/api/v1/organization")]
    [InlineData("/api/v1/branches")]
    public async Task SliceOne_DoesNotExposeOrganizationAdministrationRoutes(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record HealthPayload(string Status, string Service);

    private sealed record CurrentUserPayload(bool IsAuthenticated, string? DisplayName, string[] Permissions);
}
