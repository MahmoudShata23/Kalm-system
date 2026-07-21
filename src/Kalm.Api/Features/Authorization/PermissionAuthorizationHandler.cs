using Kalm.Api.Features.Authentication;
using Kalm.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Kalm.Api.Features.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !PermissionCatalogue.Contains(requirement.PermissionCode))
        {
            return Task.CompletedTask;
        }

        EffectiveAuthorizationSnapshot? snapshot = CurrentSnapshot();
        if (snapshot?.Permissions.Contains(requirement.PermissionCode) == true)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private EffectiveAuthorizationSnapshot? CurrentSnapshot()
        => _httpContextAccessor.HttpContext?.Items[ManagementAuthenticationConstants.SessionItemKey] is ManagementSessionSnapshot session
            ? session.Authorization
            : null;
}
