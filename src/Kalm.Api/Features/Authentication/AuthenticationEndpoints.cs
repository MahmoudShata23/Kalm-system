using System.Security.Claims;
using Kalm.Api.Configuration;
using Kalm.Api.Transactions;
using Kalm.Identity.Application.ManagementAuthentication;
using Kalm.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kalm.Api.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.MapGet("/csrf", GetCsrfAsync).AllowAnonymous().WithName("GetCsrfToken").Produces<CsrfTokenResponse>();
        group.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(ManagementAuthenticationConstants.LoginRateLimitPolicy).WithName("ManagementLogin")
            .Produces<CurrentUserResponse>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status429TooManyRequests);
        group.MapPost("/logout", LogoutAsync).AllowAnonymous().WithName("ManagementLogout")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        group.MapGet("/me", GetCurrentUser).AllowAnonymous().WithName("GetCurrentUser").Produces<CurrentUserResponse>();
        return endpoints;
    }

    private static IResult GetCsrfAsync(HttpContext context, IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        return Results.Ok(new CsrfTokenResponse(tokens.RequestToken ?? throw new InvalidOperationException("Antiforgery request token was not generated.")));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        HttpContext context,
        IAntiforgery antiforgery,
        ManagementAuthenticationAuditTransactionCoordinator coordinator,
        IOptions<ManagementAuthenticationOptions> options,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        try
        {
            IResult? csrfFailure = await ValidateCsrfAsync(context, antiforgery);
            if (csrfFailure is not null)
            {
                return csrfFailure;
            }

            if (string.IsNullOrWhiteSpace(request.Identifier) || request.Identifier.Length > 254)
            {
                return Problem(StatusCodes.Status422UnprocessableEntity, "auth.validation_failed", "Identifier is required and cannot exceed 254 characters.");
            }

            try
            {
                PasswordPolicy.Validate(request.Password);
            }
            catch (ArgumentException)
            {
                return Problem(StatusCodes.Status422UnprocessableEntity, "auth.validation_failed", "Password must contain between 15 and 128 Unicode characters.");
            }

            ManagementLoginResult result = await coordinator.LoginAsync(
                request.Identifier, request.Password, context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(), context.TraceIdentifier, cancellationToken);
            if (!result.Succeeded || result.Session is null)
            {
                return Problem(StatusCodes.Status401Unauthorized, "auth.invalid_credentials", "The identifier or password is invalid, or the account is unavailable.");
            }

            var identity = new ClaimsIdentity(ManagementAuthenticationConstants.Scheme);
            identity.AddClaim(new Claim(ManagementAuthenticationConstants.SessionIdClaim, result.SessionId.ToString("N")));
            identity.AddClaim(new Claim(ManagementAuthenticationConstants.SchemeVersionClaim, ManagementAuthenticationConstants.SchemeVersion));
            await context.SignInAsync(
                ManagementAuthenticationConstants.Scheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    AllowRefresh = false,
                    IsPersistent = false,
                    ExpiresUtc = result.Session.AbsoluteExpiresAtUtc
                });

            return Results.Ok(ToResponse(result.Session, options.Value));
        }
        finally
        {
            request.Password = string.Empty;
        }
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        [FromHeader(Name = "X-XSRF-TOKEN")] string? csrfHeader,
        IAntiforgery antiforgery,
        ManagementAuthenticationAuditTransactionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        _ = csrfHeader;
        IResult? csrfFailure = await ValidateCsrfAsync(context, antiforgery);
        if (csrfFailure is not null)
        {
            return csrfFailure;
        }

        ManagementSessionSnapshot? session = CurrentSession(context);
        if (session is not null)
        {
            bool revoked = await coordinator.LogoutAsync(
                session.SessionId, context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent.ToString(),
                context.TraceIdentifier, cancellationToken);
            if (!revoked)
            {
                return Problem(StatusCodes.Status409Conflict, "auth.session_changed", "The session could not be revoked.");
            }
        }

        await context.SignOutAsync(ManagementAuthenticationConstants.Scheme);
        return Results.NoContent();
    }

    private static IResult GetCurrentUser(HttpContext context, IOptions<ManagementAuthenticationOptions> options)
    {
        ManagementSessionSnapshot? session = CurrentSession(context);
        return session is null
            ? Results.Ok(new CurrentUserResponse(false, null, null, null, null, null, null, []))
            : Results.Ok(ToResponse(session, options.Value));
    }

    private static ManagementSessionSnapshot? CurrentSession(HttpContext context)
        => context.User.Identity?.IsAuthenticated == true
            ? context.Items[ManagementAuthenticationConstants.SessionItemKey] as ManagementSessionSnapshot
            : null;

    private static CurrentUserResponse ToResponse(ManagementSessionSnapshot session, ManagementAuthenticationOptions options)
        => new(
            true, session.Username, session.DisplayName, session.PreferredLanguage,
            session.InactivityExpiresAtUtc, session.AbsoluteExpiresAtUtc,
            session.LastReauthenticatedAtUtc.AddMinutes(options.ReauthenticationMinutes), []);

    private static async Task<IResult?> ValidateCsrfAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return Problem(StatusCodes.Status400BadRequest, "auth.csrf_invalid", "The antiforgery token is missing or invalid.");
        }
    }

    private static IResult Problem(int status, string code, string detail)
        => Results.Problem(statusCode: status, title: "Authentication request failed", detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
