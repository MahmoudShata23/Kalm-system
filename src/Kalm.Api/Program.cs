using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Health;
using Kalm.Api.Infrastructure.Correlation;
using Kalm.Api.Infrastructure.ProblemDetails;
using Kalm.Api.Persistence;
using Kalm.Api.Transactions;
using Kalm.BuildingBlocks.Time;
using Kalm.SharedKernel.Time;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options => options.ConfigureKalmProblemDetails());
builder.Services.AddOpenApi();
builder.Services.AddKalmDatabase(builder.Configuration);
builder.Services.AddScoped<SliceOneOrganizationAuditTransactionCoordinator>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHealthChecks()
    .AddCheck<PostgreSqlReadinessCheck>("postgresql");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Redirect("/health/live", permanent: false))
    .ExcludeFromDescription();

app.MapGet("/health/live", () => Results.Ok(new HealthResponse("Healthy", "Kalm.Api")))
    .WithName("Liveness")
    .WithTags("Health");

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new HealthResponse(report.Status.ToString(), "Kalm.Api"));
    }
})
.WithName("Readiness")
.WithTags("Health");

app.MapAuthenticationEndpoints();

app.Run();

public partial class Program;
