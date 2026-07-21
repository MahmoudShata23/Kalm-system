using Kalm.Api.Features.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Kalm.Api.Features.Authorization;

public sealed class OperationalBranchAuthorizationHandler : AuthorizationHandler<OperationalBranchRequirement, OperationalBranchResource>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OperationalBranchAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationalBranchRequirement requirement,
        OperationalBranchResource resource)
    {
        EffectiveAuthorizationSnapshot? snapshot = CurrentSnapshot();
        if (context.User.Identity?.IsAuthenticated == true
            && snapshot is not null
            && snapshot.OrganizationId == resource.OrganizationId
            && snapshot.BranchAccess?.OperationalBranchIds.Contains(resource.BranchId) == true)
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
