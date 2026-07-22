using Kalm.Identity;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Features.UserAdministration;

public sealed class UserAdministrationQueries
{
    private readonly IdentityDbContext _identity;
    private readonly OrganizationDbContext _organization;

    public UserAdministrationQueries(IdentityDbContext identity, OrganizationDbContext organization)
    {
        _identity = identity;
        _organization = organization;
    }

    public async Task<UserListResponse> ListAsync(
        Guid organizationId,
        string status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<User> users = _identity.Users.AsNoTracking().Where(user => user.OrganizationId == organizationId);
        users = status switch
        {
            "active" => users.Where(user => user.Status == UserStatus.Active),
            "suspended" => users.Where(user => user.Status == UserStatus.Suspended),
            "archived" => users.Where(user => user.Status == UserStatus.Archived),
            _ => users
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            string normalized = term.Normalize().ToUpperInvariant();
            users = users.Where(user => user.NormalizedUsername.Contains(normalized)
                || (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalized))
                || EF.Functions.ILike(user.DisplayName, $"%{term}%"));
        }

        int totalCount = await users.CountAsync(cancellationToken);
        var pageItems = await users
            .OrderBy(user => user.NormalizedUsername)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new
            {
                user.Id,
                user.Username,
                user.Email,
                user.DisplayName,
                user.PreferredLanguage,
                user.Status,
                user.UpdatedAtUtc
            })
            .ToArrayAsync(cancellationToken);
        Guid[] userIds = pageItems.Select(user => user.Id).ToArray();
        var assignedRoles = await (
            from assignment in _identity.UserRoleAssignments.AsNoTracking()
            join role in _identity.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where userIds.Contains(assignment.UserId)
                && assignment.RevokedAtUtc == null
                && role.OrganizationId == organizationId
                && role.Status == RoleStatus.Active
            orderby role.NormalizedName, role.Id
            select new { assignment.UserId, role.Name })
            .ToArrayAsync(cancellationToken);
        ILookup<Guid, string> rolesByUser = assignedRoles.ToLookup(item => item.UserId, item => item.Name);

        UserSummaryResponse[] items = pageItems.Select(user => new UserSummaryResponse(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.PreferredLanguage,
            Status(user.Status),
            rolesByUser[user.Id].ToArray(),
            user.UpdatedAtUtc)).ToArray();
        return new UserListResponse(items, page, pageSize, totalCount);
    }

    public async Task<UserVersionedDetail?> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await _identity.Users.AsNoTracking()
            .Where(candidate => candidate.Id == userId && candidate.OrganizationId == organizationId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Username,
                candidate.Email,
                candidate.DisplayName,
                candidate.PreferredLanguage,
                candidate.Status,
                candidate.Version,
                candidate.CreatedAtUtc,
                candidate.UpdatedAtUtc,
                candidate.ActivatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        string credentialStatus = await _identity.PasswordCredentials.AsNoTracking()
            .Where(credential => credential.UserId == userId)
            .Select(credential => credential.Status == PasswordCredentialStatus.Active ? "active" : "pendingSetup")
            .SingleAsync(cancellationToken);
        Guid[] roleIds = await _identity.UserRoleAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == userId && assignment.OrganizationId == organizationId && assignment.RevokedAtUtc == null)
            .OrderBy(assignment => assignment.RoleId)
            .Select(assignment => assignment.RoleId)
            .ToArrayAsync(cancellationToken);
        UserBranchAccess? access = await _organization.UserBranchAccesses.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.OrganizationId == organizationId, cancellationToken);
        if (access is null)
        {
            return null;
        }

        Guid[] branchIds = await _organization.UserBranchAssignments.AsNoTracking()
            .Where(assignment => assignment.AccessId == access.Id && assignment.RevokedAtUtc == null)
            .OrderBy(assignment => assignment.BranchId)
            .Select(assignment => assignment.BranchId)
            .ToArrayAsync(cancellationToken);
        return new UserVersionedDetail(new UserDetailResponse(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.PreferredLanguage,
            Status(user.Status),
            credentialStatus,
            roleIds,
            Scope(access.Scope),
            branchIds,
            user.CreatedAtUtc,
            user.UpdatedAtUtc,
            user.ActivatedAtUtc), user.Version);
    }

    public async Task<UserEditorOptionsResponse> GetOptionsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        UserRoleOptionResponse[] roles = await _identity.Roles.AsNoTracking()
            .Where(role => role.OrganizationId == organizationId && role.Status == RoleStatus.Active)
            .OrderBy(role => role.NormalizedName)
            .ThenBy(role => role.Id)
            .Select(role => new UserRoleOptionResponse(role.Id, role.Name))
            .ToArrayAsync(cancellationToken);
        UserBranchOptionResponse[] branches = await _organization.Branches.AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId && branch.Status == BranchStatus.Active)
            .OrderBy(branch => branch.Code)
            .ThenBy(branch => branch.Id)
            .Select(branch => new UserBranchOptionResponse(branch.Id, branch.Name, branch.Code))
            .ToArrayAsync(cancellationToken);
        return new UserEditorOptionsResponse(roles, branches);
    }

    internal static string Status(UserStatus status) => status switch
    {
        UserStatus.Active => "active",
        UserStatus.Suspended => "suspended",
        _ => "archived"
    };

    internal static string Scope(BranchAccessScope scope)
        => scope == BranchAccessScope.AssignedBranches ? "assignedBranches" : "allOrganizationBranches";
}

public sealed record UserVersionedDetail(UserDetailResponse User, long Version);
