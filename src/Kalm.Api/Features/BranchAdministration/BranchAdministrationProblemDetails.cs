using Kalm.Organization;

namespace Kalm.Api.Features.BranchAdministration;

internal static class BranchAdministrationProblemDetails
{
    public static IResult Create(string code, long? currentVersion = null, BranchDependencyCountsResponse? dependencies = null)
    {
        (int status, string title, string detail) = code switch
        {
            "branch.not_found" => (404, "Branch not found", "The requested branch was not found."),
            "branch.validation_failed" => (422, "Branch invalid", "The branch request is invalid."),
            "branch.invalid_query" => (422, "Branch query invalid", "The branch query parameters are invalid."),
            "branch.code_reserved" => (409, "Branch code unavailable", "The normalized branch code is already reserved."),
            "branch.archived" => (409, "Branch archived", "An archived branch cannot be changed or activated."),
            "branch.dependencies_active" => (409, "Branch has active dependencies", "Resolve the active branch dependencies before deactivation."),
            "branch.precondition_required" => (428, "Precondition required", "A current strong If-Match value is required."),
            "branch.invalid_precondition" => (400, "Invalid precondition", "If-Match must contain one strong quoted branch version."),
            "branch.concurrency_conflict" => (412, "Branch changed", "The branch was changed by another request."),
            "branch.rate_limited" => (429, "Too many requests", "Wait before trying this branch mutation again."),
            "auth.csrf_invalid" => (400, "Antiforgery validation failed", "The antiforgery token is missing or invalid."),
            _ => (500, "Branch administration failed", "The operation could not be completed.")
        };

        Dictionary<string, object?> extensions = new() { ["code"] = code };
        if (currentVersion is not null)
        {
            extensions["currentEtag"] = BranchAdministrationEndpoints.FormatEtag(currentVersion.Value);
        }

        if (dependencies is not null)
        {
            extensions["dependencyCounts"] = dependencies;
        }

        return Results.Problem(statusCode: status, title: title, detail: detail, extensions: extensions);
    }
}
