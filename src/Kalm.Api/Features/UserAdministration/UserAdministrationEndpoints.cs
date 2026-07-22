using Kalm.Api.Configuration;
using Kalm.Api.Features.Authentication;
using Kalm.Api.Features.Authorization;
using Kalm.Api.Transactions;
using Kalm.Identity;
using Kalm.SharedKernel.Time;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kalm.Api.Features.UserAdministration;

public static class UserAdministrationEndpoints
{
    public const string PasswordRateLimitPolicy = "user-administration-password";

    public static IEndpointRouteBuilder MapUserAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/management/users")
            .WithTags("User Administration");

        group.MapGet("", ListUsersAsync)
            .RequireAuthorization(KalmPolicies.UserAdministrationView)
            .WithName("ListManagementUsers")
            .Produces<UserListResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapGet("/options", GetOptionsAsync)
            .RequireAuthorization(KalmPolicies.UserAdministrationView)
            .WithName("GetManagementUserEditorOptions")
            .Produces<UserEditorOptionsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        group.MapGet("/{userId:guid}", GetUserAsync)
            .RequireAuthorization(KalmPolicies.UserAdministrationView)
            .WithName("GetManagementUser")
            .Produces<UserDetailResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("", CreateUserAsync)
            .RequireAuthorization(KalmPolicies.UserAdministrationManage)
            .WithName("CreateManagementUser")
            .Produces<UserDetailResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapPut("/{userId:guid}", UpdateUserAsync)
            .RequireAuthorization(KalmPolicies.UserAdministrationManage)
            .WithName("UpdateManagementUser")
            .Produces<UserDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
        group.MapPost("/{userId:guid}/activate", ActivateUserAsync)
            .RequireAuthorization(KalmPolicies.UserAdministrationManage)
            .WithName("ActivateManagementUser")
            .Produces<UserDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
        group.MapPost("/{userId:guid}/suspend", SuspendUserAsync)
            .RequireAuthorization(KalmPolicies.UserAdministrationManage)
            .WithName("SuspendManagementUser")
            .Produces<UserDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
        group.MapPost("/{userId:guid}/password", SetPasswordAsync)
            .RequireAuthorization(KalmPolicies.UserAdministrationManage)
            .RequireRateLimiting(PasswordRateLimitPolicy)
            .WithName("SetManagementUserPassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        group.MapPost("/{userId:guid}/pin", SetPinAsync)
            .RequireAuthorization(KalmPolicies.UserAdministrationManage)
            .RequireRateLimiting(PasswordRateLimitPolicy)
            .WithName("SetManagementUserPin")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        return endpoints;
    }

    private static async Task<IResult> ListUsersAsync(
        HttpContext context,
        UserAdministrationQueries queries,
        string status = "all",
        string? search = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (status is not ("active" or "suspended" or "archived" or "all")
            || page < 1 || pageSize is < 1 or > 100 || search?.Length > 120)
        {
            return UserAdministrationProblemDetails.Create("user.validation_failed");
        }

        ManagementSessionSnapshot session = CurrentSession(context);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await queries.ListAsync(session.OrganizationId, status, search, page, pageSize, cancellationToken));
    }

    private static async Task<IResult> GetOptionsAsync(
        HttpContext context,
        UserAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        ManagementSessionSnapshot session = CurrentSession(context);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await queries.GetOptionsAsync(session.OrganizationId, cancellationToken));
    }

    private static async Task<IResult> GetUserAsync(
        Guid userId,
        HttpContext context,
        UserAdministrationQueries queries,
        CancellationToken cancellationToken)
    {
        ManagementSessionSnapshot session = CurrentSession(context);
        UserVersionedDetail? detail = await queries.GetAsync(session.OrganizationId, userId, cancellationToken);
        return detail is null ? UserAdministrationProblemDetails.Create("user.not_found") : VersionedOk(context, detail);
    }

    private static async Task<IResult> CreateUserAsync(
        UserCreateRequest request,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context,
        IAntiforgery antiforgery,
        UserAdministrationAuditTransactionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        try
        {
            if (!await IsCsrfValidAsync(context, antiforgery))
            {
                return UserAdministrationProblemDetails.Create("auth.csrf_invalid");
            }

            ManagementSessionSnapshot session = CurrentSession(context);
            UserOperationResult result = await coordinator.CreateAsync(
                session.OrganizationId, session.UserId, request, context.TraceIdentifier, cancellationToken);
            if (!result.Succeeded)
            {
                return UserAdministrationProblemDetails.Create(result.ErrorCode!, result.CurrentVersion);
            }

            UserVersionedDetail detail = result.Detail!;
            SetVersionHeaders(context, detail.Version);
            return Results.Created($"/api/v1/management/users/{detail.User.Id:D}", detail.User);
        }
        finally
        {
            request.InitialPassword = null;
        }
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid userId,
        UserUpdateRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context,
        IAntiforgery antiforgery,
        UserAdministrationAuditTransactionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        IResult? precondition = ParseRequiredEtag(ifMatch, out long version);
        if (precondition is not null) return precondition;
        if (!await IsCsrfValidAsync(context, antiforgery)) return UserAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = CurrentSession(context);
        UserOperationResult result = await coordinator.UpdateAsync(
            session.OrganizationId, session.UserId, userId, version, request, context.TraceIdentifier, cancellationToken);
        return OperationResult(context, result);
    }

    private static async Task<IResult> ActivateUserAsync(
        Guid userId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context,
        IAntiforgery antiforgery,
        UserAdministrationAuditTransactionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        IResult? precondition = ParseRequiredEtag(ifMatch, out long version);
        if (precondition is not null) return precondition;
        if (!await IsCsrfValidAsync(context, antiforgery)) return UserAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = CurrentSession(context);
        UserOperationResult result = await coordinator.ActivateAsync(
            session.OrganizationId, session.UserId, userId, version, context.TraceIdentifier, cancellationToken);
        return OperationResult(context, result);
    }

    private static async Task<IResult> SuspendUserAsync(
        Guid userId,
        UserSuspendRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context,
        IAntiforgery antiforgery,
        UserAdministrationAuditTransactionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        IResult? precondition = ParseRequiredEtag(ifMatch, out long version);
        if (precondition is not null) return precondition;
        if (!await IsCsrfValidAsync(context, antiforgery)) return UserAdministrationProblemDetails.Create("auth.csrf_invalid");
        ManagementSessionSnapshot session = CurrentSession(context);
        UserOperationResult result = await coordinator.SuspendAsync(
            session.OrganizationId, session.UserId, userId, version, request.ConfirmSelfSuspension,
            context.TraceIdentifier, cancellationToken);
        return OperationResult(context, result);
    }

    private static async Task<IResult> SetPasswordAsync(
        Guid userId,
        UserPasswordRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context,
        IAntiforgery antiforgery,
        UserAdministrationAuditTransactionCoordinator coordinator,
        IOptions<ManagementAuthenticationOptions> options,
        IClock clock,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        try
        {
            IResult? precondition = ParseRequiredEtag(ifMatch, out long version);
            if (precondition is not null) return precondition;
            if (!await IsCsrfValidAsync(context, antiforgery)) return UserAdministrationProblemDetails.Create("auth.csrf_invalid");
            ManagementSessionSnapshot session = CurrentSession(context);
            if (clock.UtcNow >= session.LastReauthenticatedAtUtc.AddMinutes(options.Value.ReauthenticationMinutes))
            {
                return UserAdministrationProblemDetails.Create("user.reauthentication_required");
            }

            UserOperationResult result = await coordinator.SetPasswordAsync(
                session.OrganizationId, session.UserId, userId, version, request.Password,
                context.TraceIdentifier, cancellationToken);
            if (!result.Succeeded)
            {
                return UserAdministrationProblemDetails.Create(result.ErrorCode!, result.CurrentVersion);
            }

            SetVersionHeaders(context, result.Detail!.Version);
            return Results.NoContent();
        }
        finally
        {
            request.Password = string.Empty;
        }
    }

    private static IResult OperationResult(HttpContext context, UserOperationResult result)
        => result.Succeeded
            ? VersionedOk(context, result.Detail!)
            : UserAdministrationProblemDetails.Create(result.ErrorCode!, result.CurrentVersion);

    private static async Task<IResult> SetPinAsync(Guid userId, UserPinRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context, IAntiforgery antiforgery, PinAdministrationAuditTransactionCoordinator coordinator,
        IOptions<ManagementAuthenticationOptions> options, IClock clock, CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        try
        {
            IResult? precondition = ParseRequiredEtag(ifMatch, out long version); if (precondition is not null) return precondition;
            if (!await IsCsrfValidAsync(context, antiforgery)) return UserAdministrationProblemDetails.Create("auth.csrf_invalid");
            ManagementSessionSnapshot session = CurrentSession(context);
            if (clock.UtcNow >= session.LastReauthenticatedAtUtc.AddMinutes(options.Value.ReauthenticationMinutes)) return UserAdministrationProblemDetails.Create("user.reauthentication_required");
            PinAdministrationResult result = await coordinator.SetAsync(session.OrganizationId, session.UserId, userId, version, request.Pin, context.TraceIdentifier, cancellationToken);
            if (!result.Succeeded) return UserAdministrationProblemDetails.Create(result.ErrorCode!, result.CurrentVersion);
            SetVersionHeaders(context, result.Version); return Results.NoContent();
        }
        finally { request.Pin = string.Empty; }
    }

    private static IResult VersionedOk(HttpContext context, UserVersionedDetail detail)
    {
        SetVersionHeaders(context, detail.Version);
        return Results.Ok(detail.User);
    }

    private static void SetVersionHeaders(HttpContext context, long version)
    {
        context.Response.Headers.ETag = FormatEtag(version);
        context.Response.Headers.CacheControl = "no-store";
    }

    internal static string FormatEtag(long version) => $"\"{version}\"";

    private static IResult? ParseRequiredEtag(string? value, out long version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return UserAdministrationProblemDetails.Create("user.precondition_required");
        }

        if (value.Length < 3 || value[0] != '"' || value[^1] != '"'
            || value.Contains(',', StringComparison.Ordinal)
            || value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(value.AsSpan(1, value.Length - 2), out version)
            || version < 1 || !string.Equals(value, FormatEtag(version), StringComparison.Ordinal))
        {
            version = 0;
            return UserAdministrationProblemDetails.Create("user.invalid_precondition");
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
