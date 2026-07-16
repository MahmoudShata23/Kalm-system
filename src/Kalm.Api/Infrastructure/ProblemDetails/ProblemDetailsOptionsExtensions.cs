using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kalm.Api.Infrastructure.ProblemDetails;

public static class ProblemDetailsOptionsExtensions
{
    public static void ConfigureKalmProblemDetails(this ProblemDetailsOptions options)
    {
        options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            if (!problem.Extensions.ContainsKey("code"))
            {
                problem.Extensions["code"] = ResolveDefaultCode(context);
            }
        };
    }

    private static string ResolveDefaultCode(ProblemDetailsContext context)
    {
        var exception = context.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is not null)
        {
            return "platform.unhandled_error";
        }

        return context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => "platform.bad_request",
            StatusCodes.Status401Unauthorized => "platform.unauthorized",
            StatusCodes.Status403Forbidden => "platform.forbidden",
            StatusCodes.Status404NotFound => "platform.not_found",
            StatusCodes.Status409Conflict => "platform.conflict",
            StatusCodes.Status422UnprocessableEntity => "platform.validation_failed",
            _ => "platform.error"
        };
    }
}
