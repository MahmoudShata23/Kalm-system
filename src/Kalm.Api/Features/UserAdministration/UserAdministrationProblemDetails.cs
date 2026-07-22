namespace Kalm.Api.Features.UserAdministration;

internal static class UserAdministrationProblemDetails
{
    public static IResult Create(string code, long? currentVersion = null)
    {
        (int status, string title, string detail) = code switch
        {
            "user.not_found" => (StatusCodes.Status404NotFound, "User not found", "The requested user was not found."),
            "user.identifier_conflict" => (StatusCodes.Status409Conflict, "User identifier conflict", "The normalized username or email is already in use."),
            "user.archived" => (StatusCodes.Status409Conflict, "User archived", "Archived users cannot be changed."),
            "user.activation_requirements_not_met" => (StatusCodes.Status409Conflict, "Activation requirements not met", "An active credential, at least one active role, and valid branch access are required."),
            "user.last_management_access" => (StatusCodes.Status409Conflict, "Management access protected", "The operation would remove the final active management-capable user."),
            "user.self_suspension_confirmation_required" => (StatusCodes.Status409Conflict, "Self-suspension confirmation required", "Explicit confirmation is required to suspend the current account."),
            "user.concurrency_conflict" => (StatusCodes.Status412PreconditionFailed, "User changed", "The user changed after it was loaded. Reload the latest version before saving."),
            "user.precondition_required" => (StatusCodes.Status428PreconditionRequired, "Precondition required", "A current strong If-Match value is required."),
            "user.invalid_precondition" => (StatusCodes.Status400BadRequest, "Invalid precondition", "If-Match must contain one strong quoted user version."),
            "user.roles_invalid" => (StatusCodes.Status422UnprocessableEntity, "Role assignment invalid", "Select one or more active roles from this organization."),
            "user.branch_access_invalid" => (StatusCodes.Status422UnprocessableEntity, "Branch access invalid", "Branch access must use active branches and match the selected scope."),
            "user.password_invalid" => (StatusCodes.Status422UnprocessableEntity, "Password invalid", "Password must contain between 15 and 128 Unicode characters."),
            "user.reauthentication_required" => (StatusCodes.Status403Forbidden, "Recent authentication required", "Sign in again before setting or resetting a password."),
            "user.validation_failed" => (StatusCodes.Status422UnprocessableEntity, "User validation failed", "The user request contains invalid data."),
            "auth.csrf_invalid" => (StatusCodes.Status400BadRequest, "Antiforgery validation failed", "The antiforgery token is missing or invalid."),
            _ => (StatusCodes.Status500InternalServerError, "User administration failed", "The user operation could not be completed.")
        };

        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (currentVersion is not null)
        {
            extensions["currentEtag"] = UserAdministrationEndpoints.FormatEtag(currentVersion.Value);
        }

        return Results.Problem(statusCode: status, title: title, detail: detail, extensions: extensions);
    }
}
