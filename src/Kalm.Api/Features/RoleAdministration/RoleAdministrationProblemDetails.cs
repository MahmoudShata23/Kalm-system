namespace Kalm.Api.Features.RoleAdministration;

internal static class RoleAdministrationProblemDetails
{
    public static IResult Create(string code, long? currentVersion = null, int? activeAssignmentCount = null)
    {
        (int status, string title, string detail) = code switch
        {
            "role.not_found" => (StatusCodes.Status404NotFound, "Role not found", "The requested role was not found."),
            "role.name_conflict" => (StatusCodes.Status409Conflict, "Role name conflict", "A role with the same normalized name already exists."),
            "role.system_role_protected" => (StatusCodes.Status409Conflict, "System role protected", "This protected system role cannot be changed through role administration."),
            "role.archived" => (StatusCodes.Status409Conflict, "Role archived", "Archived roles cannot be changed."),
            "role.has_active_assignments" => (StatusCodes.Status409Conflict, "Role has active assignments", "Remove active assignments before archiving this role."),
            "role.last_management_access" => (StatusCodes.Status409Conflict, "Management access protected", "The operation would remove the final active management-capable user."),
            "role.concurrency_conflict" => (StatusCodes.Status412PreconditionFailed, "Role changed", "The role changed after it was loaded. Reload the latest version before saving."),
            "role.precondition_required" => (StatusCodes.Status428PreconditionRequired, "Precondition required", "A current strong If-Match value is required."),
            "role.invalid_precondition" => (StatusCodes.Status400BadRequest, "Invalid precondition", "If-Match must contain one strong quoted role version."),
            "role.permission_set_required" => (StatusCodes.Status422UnprocessableEntity, "Permission set required", "An active role must have at least one permission."),
            "role.permission_set_invalid" => (StatusCodes.Status422UnprocessableEntity, "Permission set invalid", "The permission set contains an invalid, duplicate, inactive, or unavailable permission."),
            "role.validation_failed" => (StatusCodes.Status422UnprocessableEntity, "Role validation failed", "The role request contains invalid data."),
            "authorization.permission_catalogue_unavailable" => (StatusCodes.Status503ServiceUnavailable, "Permission catalogue unavailable", "Role administration is unavailable until the permission catalogue is consistent."),
            "auth.csrf_invalid" => (StatusCodes.Status400BadRequest, "Antiforgery validation failed", "The antiforgery token is missing or invalid."),
            _ => (StatusCodes.Status500InternalServerError, "Role administration failed", "The role operation could not be completed.")
        };

        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (currentVersion is not null)
        {
            extensions["currentEtag"] = RoleAdministrationEndpoints.FormatEtag(currentVersion.Value);
        }

        if (activeAssignmentCount is not null)
        {
            extensions["activeAssignmentCount"] = activeAssignmentCount.Value;
        }

        return Results.Problem(statusCode: status, title: title, detail: detail, extensions: extensions);
    }
}
