using Kalm.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Kalm.Api.Features.Authorization;

public static class KalmPolicies
{
    public const string ManagementAccess = "Kalm.ManagementAccess";
    public const string RoleAdministration = "Kalm.RoleAdministration";

    public static void AddKalmAuthorization(AuthorizationOptions options)
    {
        options.AddPolicy(ManagementAccess, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(PermissionCodes.ManagementAccess));
        });
        options.AddPolicy(RoleAdministration, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(
                new PermissionRequirement(PermissionCodes.ManagementAccess),
                new PermissionRequirement(PermissionCodes.RolesManage));
        });
    }
}
