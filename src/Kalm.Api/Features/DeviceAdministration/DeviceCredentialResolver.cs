using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Features.DeviceAdministration;

public sealed class DeviceCredentialResolver(OrganizationDbContext organization)
{
    public const string CookieName = "__Host-Kalm.Device";
    public static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(30);

    public async Task<DeviceRequestContext?> ResolveAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out string? raw) || string.IsNullOrWhiteSpace(raw)) return null;
        string hash = DeviceSecurity.Hash(raw);
        DeviceRequestContext? resolved = await (
            from credential in organization.DeviceCredentials.AsNoTracking()
            join device in organization.Devices.AsNoTracking() on credential.DeviceId equals device.Id
            join branch in organization.Branches.AsNoTracking() on new { device.BranchId, device.OrganizationId } equals new { BranchId = branch.Id, branch.OrganizationId }
            join tenant in organization.Organizations.AsNoTracking() on device.OrganizationId equals tenant.Id
            where credential.CredentialHash == hash && credential.RevokedAtUtc == null
                && credential.SecurityVersion == device.SecurityVersion && device.Status == DeviceStatus.Active
                && branch.Status == BranchStatus.Active && tenant.Status == OrganizationStatus.Active
            select new DeviceRequestContext(device.Id, device.OrganizationId, device.BranchId, device.SecurityVersion, device.Name, device.Type))
            .SingleOrDefaultAsync(cancellationToken);
        if (resolved is null) ClearCookie(context);
        return resolved;
    }

    public static void SetCookie(HttpContext context, string credential, DateTimeOffset now)
        => context.Response.Cookies.Append(CookieName, credential, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            MaxAge = CookieLifetime,
            Expires = now.Add(CookieLifetime)
        });

    public static void ClearCookie(HttpContext context)
        => context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true
        });
}

public sealed record DeviceRequestContext(Guid DeviceId, Guid OrganizationId, Guid BranchId, int SecurityVersion, string Name, DeviceType Type);
