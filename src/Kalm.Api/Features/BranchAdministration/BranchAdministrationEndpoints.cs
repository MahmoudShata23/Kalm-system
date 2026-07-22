using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;
using Kalm.Api.Transactions;
using Kalm.Organization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Kalm.Api.Features.BranchAdministration;

public static class BranchAdministrationEndpoints
{
    public const string WriteRateLimitPolicy = "branch-administration-write";

    public static IEndpointRouteBuilder MapBranchAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder management = endpoints.MapGroup("/api/v1/management/branches")
            .WithTags("Branch Administration");

        management.MapGet("", ListAsync)
            .WithName("ListManagementBranches")
            .RequireAuthorization(KalmPolicies.BranchAdministrationView)
            .Produces<BranchListResponse>()
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(422);
        management.MapGet("/{branchId:guid}", GetAsync)
            .WithName("GetManagementBranch")
            .RequireAuthorization(KalmPolicies.BranchAdministrationView)
            .Produces<BranchDetailResponse>()
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404);
        management.MapPost("", CreateAsync)
            .WithName("CreateManagementBranch")
            .RequireAuthorization(KalmPolicies.BranchAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<BranchDetailResponse>(201)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(429)
            .ProducesProblem(422);
        management.MapPut("/{branchId:guid}", UpdateAsync)
            .WithName("UpdateManagementBranch")
            .RequireAuthorization(KalmPolicies.BranchAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<BranchDetailResponse>()
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(412)
            .ProducesProblem(429)
            .ProducesProblem(422)
            .ProducesProblem(428);
        management.MapPost("/{branchId:guid}/activate", ActivateAsync)
            .WithName("ActivateManagementBranch")
            .RequireAuthorization(KalmPolicies.BranchAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<BranchDetailResponse>()
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(412)
            .ProducesProblem(429)
            .ProducesProblem(428);
        management.MapPost("/{branchId:guid}/deactivate", DeactivateAsync)
            .WithName("DeactivateManagementBranch")
            .RequireAuthorization(KalmPolicies.BranchAdministrationManage)
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<BranchDetailResponse>()
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(412)
            .ProducesProblem(429)
            .ProducesProblem(428);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        BranchAdministrationQueries queries,
        string status = "all",
        string? search = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Results.Ok(await queries.ListAsync(
                Session(context).OrganizationId,
                status,
                search,
                page,
                pageSize,
                cancellationToken));
        }
        catch (ArgumentException)
        {
            return BranchAdministrationProblemDetails.Create("branch.invalid_query");
        }
    }

    private static async Task<IResult> GetAsync(
        Guid branchId,
        HttpContext context,
        BranchAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        BranchVersionedDetail? detail = await queries.GetAsync(
            Session(context).OrganizationId,
            branchId,
            cancellationToken);
        return detail is null
            ? BranchAdministrationProblemDetails.Create("branch.not_found")
            : Versioned(context, detail);
    }

    private static async Task<IResult> CreateAsync(
        BranchWriteRequest request,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        BranchAdministrationAuditTransactionCoordinator coordinator,
        BranchAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        if (!await ValidCsrfAsync(context, antiforgery))
        {
            return BranchAdministrationProblemDetails.Create("auth.csrf_invalid");
        }

        ManagementSessionSnapshot session = Session(context);
        BranchOperationResult result = await coordinator.CreateAsync(
            session.OrganizationId,
            session.UserId,
            request,
            context.TraceIdentifier,
            cancellationToken);
        if (!result.Succeeded)
        {
            return Problem(result);
        }

        BranchVersionedDetail detail = (await queries.GetAsync(
            session.OrganizationId,
            result.BranchId,
            cancellationToken))!;
        string location = $"/api/v1/management/branches/{result.BranchId:D}";
        SetVersion(context, detail.Version);
        context.Response.Headers.Location = location;
        return Results.Created(location, detail.Branch);
    }

    private static async Task<IResult> UpdateAsync(
        Guid branchId,
        BranchWriteRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        BranchAdministrationAuditTransactionCoordinator coordinator,
        BranchAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        IResult? precondition = ParseEtag(ifMatch, out long version);
        if (precondition is not null)
        {
            return precondition;
        }

        if (!await ValidCsrfAsync(context, antiforgery))
        {
            return BranchAdministrationProblemDetails.Create("auth.csrf_invalid");
        }

        ManagementSessionSnapshot session = Session(context);
        BranchOperationResult result = await coordinator.UpdateAsync(
            session.OrganizationId,
            session.UserId,
            branchId,
            version,
            request,
            context.TraceIdentifier,
            cancellationToken);
        return await MutationResponseAsync(result, session.OrganizationId, branchId, context, queries, cancellationToken);
    }

    private static Task<IResult> ActivateAsync(
        Guid branchId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        BranchAdministrationAuditTransactionCoordinator coordinator,
        BranchAdministrationQueries queries,
        CancellationToken cancellationToken)
        => StatusAsync(branchId, ifMatch, csrf, context, antiforgery, coordinator, queries, true, cancellationToken);

    private static Task<IResult> DeactivateAsync(
        Guid branchId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        BranchAdministrationAuditTransactionCoordinator coordinator,
        BranchAdministrationQueries queries,
        CancellationToken cancellationToken)
        => StatusAsync(branchId, ifMatch, csrf, context, antiforgery, coordinator, queries, false, cancellationToken);

    private static async Task<IResult> StatusAsync(
        Guid branchId,
        string? ifMatch,
        string? csrf,
        HttpContext context,
        IAntiforgery antiforgery,
        BranchAdministrationAuditTransactionCoordinator coordinator,
        BranchAdministrationQueries queries,
        bool activate,
        CancellationToken cancellationToken)
    {
        _ = csrf;
        IResult? precondition = ParseEtag(ifMatch, out long version);
        if (precondition is not null)
        {
            return precondition;
        }

        if (!await ValidCsrfAsync(context, antiforgery))
        {
            return BranchAdministrationProblemDetails.Create("auth.csrf_invalid");
        }

        ManagementSessionSnapshot session = Session(context);
        BranchOperationResult result = activate
            ? await coordinator.ActivateAsync(session.OrganizationId, session.UserId, branchId, version, context.TraceIdentifier, cancellationToken)
            : await coordinator.DeactivateAsync(session.OrganizationId, session.UserId, branchId, version, context.TraceIdentifier, cancellationToken);
        return await MutationResponseAsync(result, session.OrganizationId, branchId, context, queries, cancellationToken);
    }

    private static async Task<IResult> MutationResponseAsync(
        BranchOperationResult result,
        Guid organizationId,
        Guid branchId,
        HttpContext context,
        BranchAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        if (!result.Succeeded)
        {
            return Problem(result);
        }

        BranchVersionedDetail detail = (await queries.GetAsync(organizationId, branchId, cancellationToken))!;
        return Versioned(context, detail);
    }

    private static IResult Problem(BranchOperationResult result)
        => BranchAdministrationProblemDetails.Create(result.ErrorCode!, result.CurrentVersion, result.Dependencies);

    private static ManagementSessionSnapshot Session(HttpContext context)
        => context.Items[ManagementAuthenticationConstants.SessionItemKey] as ManagementSessionSnapshot
            ?? throw new InvalidOperationException("Authoritative management session unavailable.");

    private static IResult Versioned(HttpContext context, BranchVersionedDetail detail)
    {
        SetVersion(context, detail.Version);
        return Results.Ok(detail.Branch);
    }

    private static void SetVersion(HttpContext context, long version)
    {
        context.Response.Headers.ETag = FormatEtag(version);
        context.Response.Headers.CacheControl = "no-store";
    }

    internal static string FormatEtag(long version) => $"\"{version}\"";

    private static IResult? ParseEtag(string? value, out long version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return BranchAdministrationProblemDetails.Create("branch.precondition_required");
        }

        if (value.Length < 3
            || value[0] != '"'
            || value[^1] != '"'
            || value.Contains(',', StringComparison.Ordinal)
            || value == "*"
            || value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(value.AsSpan(1, value.Length - 2), out version)
            || version < 1
            || value != FormatEtag(version))
        {
            return BranchAdministrationProblemDetails.Create("branch.invalid_precondition");
        }

        return null;
    }

    private static async Task<bool> ValidCsrfAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
