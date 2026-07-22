using Kalm.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Kalm.Api.Features.Authorization;

public static class KalmPolicies
{
    public const string ManagementAccess = "Kalm.ManagementAccess";
    public const string RoleAdministration = "Kalm.RoleAdministration";
    public const string UserAdministrationView = "Kalm.UserAdministrationView";
    public const string UserAdministrationManage = "Kalm.UserAdministrationManage";
    public const string DeviceAdministration = "Kalm.DeviceAdministration";

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
        options.AddPolicy(UserAdministrationView, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(
                new PermissionRequirement(PermissionCodes.ManagementAccess),
                new PermissionRequirement(PermissionCodes.UsersView));
        });
        options.AddPolicy(UserAdministrationManage, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(
                new PermissionRequirement(PermissionCodes.ManagementAccess),
                new PermissionRequirement(PermissionCodes.UsersManage));
        });
        options.AddPolicy(DeviceAdministration, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(
                new PermissionRequirement(PermissionCodes.ManagementAccess),
                new PermissionRequirement(PermissionCodes.DevicesManage));
        });
    }
}
