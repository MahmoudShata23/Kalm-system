using Kalm.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Kalm.Api.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithSummary("Returns the current authentication skeleton state.");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Foundation login skeleton. Real credential validation is Milestone 1.");

        return endpoints;
    }

    private static Ok<CurrentUserResponse> GetCurrentUser()
    {
        return TypedResults.Ok(new CurrentUserResponse(
            IsAuthenticated: false,
            DisplayName: null,
            Permissions: Array.Empty<string>()));
    }

    private static IResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Secret))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Identifier)] = ["Identifier is required."],
                [nameof(request.Secret)] = ["Secret is required."]
            });
        }

        return TypedResults.Problem(
            title: "Authentication is not configured yet.",
            detail: "Milestone 0 exposes the login contract only. Real credential validation is delivered in Milestone 1.",
            statusCode: StatusCodes.Status501NotImplemented,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "iam.not_configured"
            });
    }
}
