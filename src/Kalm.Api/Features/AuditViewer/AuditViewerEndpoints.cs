using System.Globalization;
using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;

namespace Kalm.Api.Features.AuditViewer;

public static class AuditViewerEndpoints
{
    public static IEndpointRouteBuilder MapAuditViewerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/management/audit-logs")
            .WithTags("Audit Viewer")
            .RequireAuthorization(KalmPolicies.AuditViewer);
        group.MapGet("", ListAsync).WithName("ListManagementAuditLogs")
            .Produces<AuditLogListResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(422);
        group.MapGet("/options", OptionsAsync).WithName("GetManagementAuditLogOptions")
            .Produces<AuditViewerOptionsResponse>().ProducesProblem(401).ProducesProblem(403);
        group.MapGet("/{auditLogId:guid}", GetAsync).WithName("GetManagementAuditLog")
            .Produces<AuditLogDetailResponse>().ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        AuditViewerQueries queries,
        string? fromUtc = null,
        string? toUtc = null,
        string? action = null,
        string? result = null,
        string? actorId = null,
        string? targetType = null,
        string? targetId = null,
        string? branchId = null,
        string? correlationId = null,
        string? cursor = null,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        NoStore(context);
        if (!TryUtc(fromUtc, out DateTimeOffset from) || !TryUtc(toUtc, out DateTimeOffset to) || from >= to)
            return AuditViewerProblemDetails.Create("audit.interval_required");
        if (to - from > TimeSpan.FromDays(90)) return AuditViewerProblemDetails.Create("audit.interval_too_large");
        if (pageSize is < 1 or > 100
            || action is not null && !AuditViewerQueries.TryAction(action, out _)
            || result is not null && !AuditViewerQueries.TryResult(result, out _)
            || !OptionalGuid(actorId, out Guid? parsedActor)
            || !OptionalGuid(targetId, out Guid? parsedTarget)
            || !OptionalGuid(branchId, out Guid? parsedBranch)
            || targetType is not null && (targetType.Length is < 1 or > 100 || targetType != targetType.Trim())
            || correlationId is not null && (correlationId.Length is < 1 or > 128 || correlationId != correlationId.Trim())
            || cursor?.Length > 4096)
            return AuditViewerProblemDetails.Create("audit.invalid_filter");

        ManagementSessionSnapshot session = Session(context);
        if (session.Authorization.BranchAccess is null) return AuditViewerProblemDetails.Create("audit.invalid_filter");
        var filter = new AuditViewerFilter(from, to, action, result, parsedActor, targetType, parsedTarget, parsedBranch, correlationId, cursor, pageSize);
        try
        {
            return Results.Ok(await queries.ListAsync(session.OrganizationId, session.Authorization.BranchAccess, filter, cancellationToken));
        }
        catch (AuditViewerQueryException exception)
        {
            return AuditViewerProblemDetails.Create(exception.Code);
        }
    }

    private static async Task<IResult> OptionsAsync(HttpContext context, AuditViewerQueries queries, CancellationToken cancellationToken)
    {
        NoStore(context);
        ManagementSessionSnapshot session = Session(context);
        return session.Authorization.BranchAccess is null
            ? Results.Ok(new AuditViewerOptionsResponse([], [], []))
            : Results.Ok(await queries.OptionsAsync(session.OrganizationId, session.Authorization.BranchAccess, cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid auditLogId, HttpContext context, AuditViewerQueries queries, CancellationToken cancellationToken)
    {
        NoStore(context);
        ManagementSessionSnapshot session = Session(context);
        if (session.Authorization.BranchAccess is null) return AuditViewerProblemDetails.Create("audit.not_found");
        AuditLogDetailResponse? detail = await queries.GetAsync(session.OrganizationId, session.Authorization.BranchAccess, auditLogId, cancellationToken);
        return detail is null ? AuditViewerProblemDetails.Create("audit.not_found") : Results.Ok(detail);
    }

    private static bool TryUtc(string? value, out DateTimeOffset parsed)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out parsed)
            && parsed.Offset == TimeSpan.Zero;

    private static bool OptionalGuid(string? value, out Guid? parsed)
    {
        if (value is null) { parsed = null; return true; }
        if (Guid.TryParse(value, out Guid id) && id != Guid.Empty) { parsed = id; return true; }
        parsed = null;
        return false;
    }

    private static ManagementSessionSnapshot Session(HttpContext context)
        => context.Items[ManagementAuthenticationConstants.SessionItemKey] as ManagementSessionSnapshot
            ?? throw new InvalidOperationException("Authoritative management session unavailable.");

    private static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }
}
