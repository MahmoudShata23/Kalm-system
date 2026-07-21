using Kalm.Identity.Authorization;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Features.Authorization;

public sealed class EffectiveAuthorizationResolver
{
    private readonly IdentityDbContext _identity;
    private readonly OrganizationDbContext _organization;

    public EffectiveAuthorizationResolver(IdentityDbContext identity, OrganizationDbContext organization)
    {
        _identity = identity;
        _organization = organization;
    }

    public async Task<EffectiveAuthorizationSnapshot> ResolveAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        string[] databaseCodes = await (
            from assignment in _identity.UserRoleAssignments.AsNoTracking()
            join role in _identity.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            join grant in _identity.RolePermissions.AsNoTracking() on role.Id equals grant.RoleId
            join permission in _identity.Permissions.AsNoTracking() on grant.PermissionId equals permission.Id
            where assignment.UserId == userId
                && assignment.OrganizationId == organizationId
                && assignment.RevokedAtUtc == null
                && role.OrganizationId == organizationId
                && role.Status == RoleStatus.Active
                && grant.RevokedAtUtc == null
                && permission.Status == PermissionStatus.Active
            select permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToArrayAsync(cancellationToken);

        string[] knownCodes = databaseCodes.Where(PermissionCatalogue.Contains).ToArray();
        if (knownCodes.Length == 0)
        {
            return EffectiveAuthorizationSnapshot.Empty(userId, organizationId);
        }

        UserBranchAccess? access = await _organization.UserBranchAccesses.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.OrganizationId == organizationId, cancellationToken);
        if (access is null)
        {
            return EffectiveAuthorizationSnapshot.Empty(userId, organizationId);
        }

        Guid[] branchIds;
        if (access.Scope == BranchAccessScope.AssignedBranches)
        {
            branchIds = await _organization.UserBranchAssignments.AsNoTracking()
                .Where(assignment => assignment.AccessId == access.Id
                    && assignment.OrganizationId == organizationId
                    && assignment.RevokedAtUtc == null)
                .Select(assignment => assignment.BranchId)
                .Distinct()
                .OrderBy(branchId => branchId)
                .ToArrayAsync(cancellationToken);
            if (branchIds.Length == 0)
            {
                return EffectiveAuthorizationSnapshot.Empty(userId, organizationId);
            }
        }
        else if (access.Scope == BranchAccessScope.AllOrganizationBranches)
        {
            branchIds = await _organization.Branches.AsNoTracking()
                .Where(branch => branch.OrganizationId == organizationId)
                .Select(branch => branch.Id)
                .OrderBy(branchId => branchId)
                .ToArrayAsync(cancellationToken);
        }
        else
        {
            return EffectiveAuthorizationSnapshot.Empty(userId, organizationId);
        }

        bool organizationActive = await _organization.Organizations.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == organizationId && candidate.Status == OrganizationStatus.Active, cancellationToken);
        Guid[] operationalBranchIds = organizationActive
            ? await _organization.Branches.AsNoTracking()
                .Where(branch => branch.OrganizationId == organizationId
                    && branch.Status == BranchStatus.Active
                    && branchIds.Contains(branch.Id))
                .Select(branch => branch.Id)
                .OrderBy(branchId => branchId)
                .ToArrayAsync(cancellationToken)
            : [];

        string scope = access.Scope == BranchAccessScope.AssignedBranches ? "assignedBranches" : "allOrganizationBranches";
        return new EffectiveAuthorizationSnapshot(
            userId,
            organizationId,
            new HashSet<string>(knownCodes, StringComparer.Ordinal),
            new EffectiveBranchAccessSnapshot(scope, branchIds, new HashSet<Guid>(operationalBranchIds)));
    }
}
