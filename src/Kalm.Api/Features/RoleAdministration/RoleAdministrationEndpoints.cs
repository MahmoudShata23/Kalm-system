using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;
using Kalm.Api.Transactions;
using Kalm.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Kalm.Api.Features.RoleAdministration;

public static class RoleAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapRoleAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/management")
            .WithTags("Role Administration")
            .RequireAuthorization(KalmPolicies.RoleAdministration);

        group.MapGet("/roles", ListRolesAsync)
            .WithName("ListManagementRoles")
            .Produces<RoleListResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapGet("/roles/{roleId:guid}", GetRoleAsync)
            .WithName("GetManagementRole")
            .Produces<RoleDetailResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/roles", CreateRoleAsync)
            .WithName("CreateManagementRole")
            .Produces<RoleDetailResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapPut("/roles/{roleId:guid}", UpdateRoleAsync)
            .WithName("UpdateManagementRole")
            .Produces<RoleDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/roles/{roleId:guid}/archive", ArchiveRoleAsync)
            .WithName("ArchiveManagementRole")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
        group.MapGet("/permissions", GetPermissionsAsync)
            .WithName("GetManagementPermissions")
            .Produces<PermissionCatalogueResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> ListRolesAsync(
        HttpContext context,
        RoleAdministrationQueries queries,
        string status = "active",
        string? search = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (status is not ("active" or "archived" or "all")
            || page < 1
            || pageSize is < 1 or > 100
            || search?.Length > 120)
        {
            return RoleAdministrationProblemDetails.Create("role.validation_failed");
        }

        ManagementSessionSnapshot session = CurrentSession(context);
        return Results.Ok(await queries.ListAsync(session.OrganizationId, status, search, page, pageSize, cancellationToken));
    }

    private static async Task<IResult> GetRoleAsync(
        Guid roleId,
        HttpContext context,
        RoleAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        ManagementSessionSnapshot session = CurrentSession(context);
        RoleVersionedDetail? detail = await queries.GetAsync(session.OrganizationId, roleId, cancellationToken);
        if (detail is null)
        {
            return RoleAdministrationProblemDetails.Create("role.not_found");
        }

        context.Response.Headers.ETag = FormatEtag(detail.Version);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(detail.Role);
    }

    private static async Task<IResult> CreateRoleAsync(
        RoleWriteRequest request,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context,
        IAntiforgery antiforgery,
        RoleAdministrationAuditTransactionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        if (!await IsCsrfValidAsync(context, antiforgery))
        {
            return RoleAdministrationProblemDetails.Create("auth.csrf_invalid");
        }

        ManagementSessionSnapshot session = CurrentSession(context);
        RoleOperationResult result = await coordinator.CreateAsync(
            session.OrganizationId, session.UserId, request, context.TraceIdentifier, cancellationToken);
        if (!result.Succeeded)
        {
            return RoleAdministrationProblemDetails.Create(result.ErrorCode!, result.CurrentVersion, result.ActiveAssignmentCount);
        }

        RoleVersionedDetail detail = result.Detail!;
        context.Response.Headers.ETag = FormatEtag(detail.Version);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Created($"/api/v1/management/roles/{detail.Role.Id:D}", detail.Role);
    }

    private static async Task<IResult> UpdateRoleAsync(
        Guid roleId,
        RoleWriteRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context,
        IAntiforgery antiforgery,
        RoleAdministrationAuditTransactionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        IResult? preconditionFailure = ParseRequiredEtag(ifMatch, out long expectedVersion);
        if (preconditionFailure is not null)
        {
            return preconditionFailure;
        }

        if (!await IsCsrfValidAsync(context, antiforgery))
        {
            return RoleAdministrationProblemDetails.Create("auth.csrf_invalid");
        }

        ManagementSessionSnapshot session = CurrentSession(context);
        RoleOperationResult result = await coordinator.UpdateAsync(
            session.OrganizationId, session.UserId, roleId, expectedVersion, request, context.TraceIdentifier, cancellationToken);
        if (!result.Succeeded)
        {
            return RoleAdministrationProblemDetails.Create(result.ErrorCode!, result.CurrentVersion, result.ActiveAssignmentCount);
        }

        RoleVersionedDetail detail = result.Detail!;
        context.Response.Headers.ETag = FormatEtag(detail.Version);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(detail.Role);
    }

    private static async Task<IResult> ArchiveRoleAsync(
        Guid roleId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context,
        IAntiforgery antiforgery,
        RoleAdministrationAuditTransactionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        IResult? preconditionFailure = ParseRequiredEtag(ifMatch, out long expectedVersion);
        if (preconditionFailure is not null)
        {
            return preconditionFailure;
        }

        if (!await IsCsrfValidAsync(context, antiforgery))
        {
            return RoleAdministrationProblemDetails.Create("auth.csrf_invalid");
        }

        ManagementSessionSnapshot session = CurrentSession(context);
        RoleOperationResult result = await coordinator.ArchiveAsync(
            session.OrganizationId, session.UserId, roleId, expectedVersion, context.TraceIdentifier, cancellationToken);
        return result.Succeeded
            ? Results.NoContent()
            : RoleAdministrationProblemDetails.Create(result.ErrorCode!, result.CurrentVersion, result.ActiveAssignmentCount);
    }

    private static async Task<IResult> GetPermissionsAsync(
        HttpContext context,
        RoleAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        PermissionCatalogueResponse? catalogue = await queries.GetPermissionCatalogueAsync(cancellationToken);
        if (catalogue is null)
        {
            return RoleAdministrationProblemDetails.Create("authorization.permission_catalogue_unavailable");
        }

        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(catalogue);
    }

    internal static string FormatEtag(long version) => $"\"{version}\"";

    private static IResult? ParseRequiredEtag(string? value, out long version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return RoleAdministrationProblemDetails.Create("role.precondition_required");
        }

        if (value.Length < 3
            || value[0] != '"'
            || value[^1] != '"'
            || value.Contains(',', StringComparison.Ordinal)
            || value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(value.AsSpan(1, value.Length - 2), out version)
            || version < 1
            || !string.Equals(value, FormatEtag(version), StringComparison.Ordinal))
        {
            version = 0;
            return RoleAdministrationProblemDetails.Create("role.invalid_precondition");
        }

        return null;
    }

    private static ManagementSessionSnapshot CurrentSession(HttpContext context)
        => context.Items[ManagementAuthenticationConstants.SessionItemKey] as ManagementSessionSnapshot
            ?? throw new InvalidOperationException("The authoritative management session is unavailable.");

    private static async Task<bool> IsCsrfValidAsync(HttpContext context, IAntiforgery antiforgery)
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
