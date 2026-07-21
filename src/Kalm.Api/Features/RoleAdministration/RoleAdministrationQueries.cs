using Kalm.Identity;
using Kalm.Identity.Authorization;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Features.RoleAdministration;

public sealed class RoleAdministrationQueries
{
    private readonly IdentityDbContext _identity;

    public RoleAdministrationQueries(IdentityDbContext identity)
    {
        _identity = identity;
    }

    public async Task<RoleListResponse> ListAsync(
        Guid organizationId,
        string status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Role> roles = _identity.Roles.AsNoTracking()
            .Where(role => role.OrganizationId == organizationId);

        roles = status switch
        {
            "active" => roles.Where(role => role.Status == RoleStatus.Active),
            "archived" => roles.Where(role => role.Status == RoleStatus.Archived),
            _ => roles
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            string normalized = search.Trim().Normalize().ToUpperInvariant();
            roles = roles.Where(role => role.NormalizedName.Contains(normalized));
        }

        int totalCount = await roles.CountAsync(cancellationToken);
        RoleSummaryResponse[] items = await roles
            .OrderBy(role => role.NormalizedName)
            .ThenBy(role => role.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(role => new RoleSummaryResponse(
                role.Id,
                role.Name,
                role.Status == RoleStatus.Active ? "active" : "archived",
                role.SystemKey != null,
                _identity.RolePermissions.Count(grant => grant.RoleId == role.Id && grant.RevokedAtUtc == null),
                _identity.UserRoleAssignments.Count(assignment => assignment.RoleId == role.Id && assignment.RevokedAtUtc == null),
                role.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new RoleListResponse(items, page, pageSize, totalCount);
    }

    public async Task<RoleVersionedDetail?> GetAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = await _identity.Roles.AsNoTracking()
            .Where(candidate => candidate.Id == roleId && candidate.OrganizationId == organizationId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.Status,
                candidate.SystemKey,
                candidate.Version,
                candidate.CreatedAtUtc,
                candidate.UpdatedAtUtc,
                candidate.ArchivedAtUtc,
                ActiveAssignmentCount = _identity.UserRoleAssignments.Count(assignment => assignment.RoleId == candidate.Id && assignment.RevokedAtUtc == null)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (role is null)
        {
            return null;
        }

        string[] permissionCodes = await (
            from grant in _identity.RolePermissions.AsNoTracking()
            join permission in _identity.Permissions.AsNoTracking() on grant.PermissionId equals permission.Id
            where grant.RoleId == roleId && grant.RevokedAtUtc == null
            orderby permission.Code
            select permission.Code)
            .ToArrayAsync(cancellationToken);

        return new RoleVersionedDetail(
            new RoleDetailResponse(
                role.Id,
                role.Name,
                role.Status == RoleStatus.Active ? "active" : "archived",
                role.SystemKey is not null,
                role.ActiveAssignmentCount,
                permissionCodes,
                role.CreatedAtUtc,
                role.UpdatedAtUtc,
                role.ArchivedAtUtc),
            role.Version);
    }

    public async Task<PermissionCatalogueResponse?> GetPermissionCatalogueAsync(CancellationToken cancellationToken)
    {
        string[] databaseCodes = await _identity.Permissions.AsNoTracking()
            .Where(permission => permission.Status == PermissionStatus.Active)
            .Select(permission => permission.Code)
            .OrderBy(code => code)
            .ToArrayAsync(cancellationToken);
        string[] compiledCodes = PermissionCatalogue.AllCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray();
        string[] presentationCodes = PermissionPresentationCatalogue.All.Select(entry => entry.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        if (!databaseCodes.SequenceEqual(compiledCodes, StringComparer.Ordinal)
            || !presentationCodes.SequenceEqual(compiledCodes, StringComparer.Ordinal))
        {
            return null;
        }

        PermissionPresentationResponse[] permissions = PermissionPresentationCatalogue.All
            .OrderBy(entry => entry.GroupOrder)
            .ThenBy(entry => entry.ItemOrder)
            .ThenBy(entry => entry.Code, StringComparer.Ordinal)
            .Select(entry => new PermissionPresentationResponse(
                entry.Code,
                entry.GroupCode,
                entry.GroupOrder,
                entry.ItemOrder,
                entry.EnglishLabel,
                entry.EnglishDescription,
                entry.ArabicLabel,
                entry.ArabicDescription))
            .ToArray();
        return new PermissionCatalogueResponse(PermissionPresentationCatalogue.Version, permissions);
    }
}

public sealed record RoleVersionedDetail(RoleDetailResponse Role, long Version);
