using Kalm.Api.Configuration;
using Kalm.Api.Features.DeviceAdministration;
using Kalm.Api.Features.Authorization;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Kalm.SharedKernel.Time;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalm.Api.Features.Authentication;

public sealed class ManagementCookieEvents : CookieAuthenticationEvents
{
    private readonly IdentityDbContext _identity;
    private readonly IClock _clock;
    private readonly ManagementAuthenticationOptions _options;
    private readonly EffectiveAuthorizationResolver _authorizationResolver;
    private readonly OrganizationDbContext _organization;

    public ManagementCookieEvents(
        IdentityDbContext identity,
        IClock clock,
        IOptions<ManagementAuthenticationOptions> options,
        EffectiveAuthorizationResolver authorizationResolver,
        OrganizationDbContext organization)
    {
        _identity = identity;
        _clock = clock;
        _options = options.Value;
        _authorizationResolver = authorizationResolver;
        _organization = organization;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        string? rawSessionId = context.Principal?.FindFirst(ManagementAuthenticationConstants.SessionIdClaim)?.Value;
        string? marker = context.Principal?.FindFirst(ManagementAuthenticationConstants.SchemeVersionClaim)?.Value;
        if (!Guid.TryParseExact(rawSessionId, "N", out Guid sessionId) || marker != ManagementAuthenticationConstants.SchemeVersion)
        {
            context.RejectPrincipal();
            return;
        }

        DateTimeOffset now = _clock.UtcNow;
        DateTimeOffset requestedIdleExpiry = now.AddMinutes(_options.InactivityMinutes);
        int updated = await _identity.UserSessions
            .Where(session => session.Id == sessionId
                && session.RevokedAtUtc == null
                && now < session.InactivityExpiresAtUtc
                && now < session.AbsoluteExpiresAtUtc
                && now >= session.LastActivityAtUtc
                && _identity.Users.Any(user => user.Id == session.UserId && user.Status == UserStatus.Active)
                && ((session.DeviceId == null && _identity.PasswordCredentials.Any(credential => credential.UserId == session.UserId && credential.Status == PasswordCredentialStatus.Active))
                    || (session.DeviceId != null
                        && _identity.PinCredentials.Any(credential => credential.UserId == session.UserId && credential.Version == session.PinCredentialVersion)
                        && _identity.Users.Any(user => user.Id == session.UserId && user.AuthorizationVersion == session.AuthorizationVersion))))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.LastActivityAtUtc, now)
                .SetProperty(session => session.InactivityExpiresAtUtc, session => session.AbsoluteExpiresAtUtc < requestedIdleExpiry ? session.AbsoluteExpiresAtUtc : requestedIdleExpiry)
                .SetProperty(session => session.Version, session => session.Version + 1), context.HttpContext.RequestAborted);

        if (updated != 1)
        {
            context.RejectPrincipal();
            return;
        }

        ManagementSessionSnapshot? snapshot = await (
            from session in _identity.UserSessions.AsNoTracking()
            join user in _identity.Users.AsNoTracking() on session.UserId equals user.Id
            where session.Id == sessionId
            select new ManagementSessionSnapshot(
                session.Id, user.Id, user.OrganizationId, user.Username, user.DisplayName, user.PreferredLanguage,
                session.InactivityExpiresAtUtc, session.AbsoluteExpiresAtUtc, session.LastReauthenticatedAtUtc,
                EffectiveAuthorizationSnapshot.Empty(user.Id, user.OrganizationId)))
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (snapshot is null)
        {
            context.RejectPrincipal();
            return;
        }

        var binding = await _identity.UserSessions.AsNoTracking().Where(session => session.Id == sessionId)
            .Select(session => new { session.DeviceId, session.BranchId, session.DeviceSecurityVersion }).SingleAsync(context.HttpContext.RequestAborted);
        if (binding.DeviceId is not null)
        {
            bool validDevice = await _organization.Devices.AsNoTracking().AnyAsync(device => device.Id == binding.DeviceId
                && device.OrganizationId == snapshot.OrganizationId && device.BranchId == binding.BranchId
                && device.SecurityVersion == binding.DeviceSecurityVersion && device.Status == DeviceStatus.Active
                && _organization.Branches.Any(branch => branch.Id == device.BranchId && branch.OrganizationId == device.OrganizationId && branch.Status == BranchStatus.Active), context.HttpContext.RequestAborted);
            if (!validDevice) { DeviceCredentialResolver.ClearCookie(context.HttpContext); context.RejectPrincipal(); return; }
        }

        EffectiveAuthorizationSnapshot authorization = await _authorizationResolver.ResolveAsync(
            snapshot.UserId, snapshot.OrganizationId, context.HttpContext.RequestAborted);
        if (binding.BranchId is not null && authorization.BranchAccess?.OperationalBranchIds.Contains(binding.BranchId.Value) != true)
        {
            context.RejectPrincipal();
            return;
        }
        context.HttpContext.Items[ManagementAuthenticationConstants.SessionItemKey] = snapshot with { Authorization = authorization };
        context.ShouldRenew = false;
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
