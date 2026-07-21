using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Kalm.Api.Configuration;
using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;
using Kalm.Api.Features.Health;
using Kalm.Api.Infrastructure.Correlation;
using Kalm.Api.Infrastructure.ProblemDetails;
using Kalm.Api.Persistence;
using Kalm.Api.Transactions;
using Kalm.BuildingBlocks.Time;
using Kalm.SharedKernel.Time;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["SecurityFingerprint:ActiveKeyBase64"]))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["SecurityFingerprint:ActiveKeyVersion"] = "1",
        ["SecurityFingerprint:ActiveKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    });
}

builder.AddKalmDataProtection();
builder.Services.AddProblemDetails(options => options.ConfigureKalmProblemDetails());
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["ManagementCookie"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = ManagementAuthenticationConstants.CookieName,
        Description = "Opaque server-maintained management session cookie."
    };
    return Task.CompletedTask;
}));
builder.Services.AddKalmDatabase(builder.Configuration);
static string ResolveConnectionString(IServiceProvider provider)
    => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value.ConnectionString;
builder.Services.AddOrganizationInfrastructure(ResolveConnectionString);
builder.Services.AddAuditInfrastructure(ResolveConnectionString);
builder.Services.AddIdentityInfrastructure(builder.Configuration, ResolveConnectionString);
builder.Services.AddOptions<ManagementAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(ManagementAuthenticationOptions.SectionName))
    .Validate(options => options.InactivityMinutes > 0
        && options.AbsoluteLifetimeHours > 0
        && options.ReauthenticationMinutes > 0
        && options.FailureThreshold > 0
        && options.FailureWindowMinutes > 0
        && options.LockoutMinutes > 0
        && options.LoginRequestsPerMinute > 0, "Management authentication settings must be positive.")
    .ValidateOnStart();
builder.Services.AddScoped<SliceOneOrganizationAuditTransactionCoordinator>();
builder.Services.AddScoped<ManagementAuthenticationAuditTransactionCoordinator>();
builder.Services.AddScoped<ManagementCookieEvents>();
builder.Services.AddScoped<EffectiveAuthorizationResolver>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OperationalBranchAuthorizationHandler>();
builder.Services.AddSingleton<DummyPasswordHash>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-Kalm.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.HeaderName = "X-XSRF-TOKEN";
});
builder.Services.AddAuthentication(ManagementAuthenticationConstants.Scheme)
    .AddCookie(ManagementAuthenticationConstants.Scheme, options =>
    {
        options.Cookie.Name = ManagementAuthenticationConstants.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.EventsType = typeof(ManagementCookieEvents);
    });
builder.Services.AddOptions<CookieAuthenticationOptions>(ManagementAuthenticationConstants.Scheme)
    .Configure<IClock>((options, clock) => options.TimeProvider = new ClockTimeProvider(clock));
builder.Services.AddAuthorization(KalmPolicies.AddKalmAuthorization);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ManagementAuthenticationConstants.LoginRateLimitPolicy, context =>
    {
        int permitLimit = context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<ManagementAuthenticationOptions>>().Value.LoginRequestsPerMinute;
        string source = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(source, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });
});
builder.Services.AddHealthChecks().AddCheck<PostgreSqlReadinessCheck>("postgresql");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Redirect("/health/live", permanent: false)).ExcludeFromDescription();
app.MapGet("/health/live", () => Results.Ok(new HealthResponse("Healthy", "Kalm.Api"))).WithName("Liveness").WithTags("Health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new HealthResponse(report.Status.ToString(), "Kalm.Api"));
    }
}).WithName("Readiness").WithTags("Health");
app.MapAuthenticationEndpoints();

app.Run();

public partial class Program;
