using Kalm.Api.Features.DeviceAdministration;
using Kalm.Identity.Domain;
using Kalm.Identity.Infrastructure.Persistence;
using Kalm.Organization.Domain;
using Kalm.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kalm.Api.Features.Authentication;

public sealed class DeviceAuthenticationQueries(IdentityDbContext identity, OrganizationDbContext organization)
{
    public async Task<EligibleEmployeesResponse> EligibleAsync(DeviceRequestContext device, CancellationToken token)
    {
        Guid[] roleUsers = await (
            from assignment in identity.UserRoleAssignments.AsNoTracking()
            join role in identity.Roles.AsNoTracking() on new { assignment.RoleId, assignment.OrganizationId } equals new { RoleId = role.Id, role.OrganizationId }
            where assignment.OrganizationId == device.OrganizationId && assignment.RevokedAtUtc == null && role.Status == RoleStatus.Active
            select assignment.UserId).Distinct().ToArrayAsync(token);
        Guid[] pinUsers = await identity.PinCredentials.AsNoTracking().Select(pin => pin.UserId).ToArrayAsync(token);
        Guid[] branchUsers = await EligibleBranchUserIdsAsync(device, token);
        Guid[] eligibleIds = roleUsers.Intersect(pinUsers).Intersect(branchUsers).ToArray();
        EligibleEmployeeResponse[] employees = await identity.Users.AsNoTracking()
            .Where(user => eligibleIds.Contains(user.Id) && user.OrganizationId == device.OrganizationId && user.Status == UserStatus.Active)
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Select(user => new EligibleEmployeeResponse(user.Id, user.DisplayName)).ToArrayAsync(token);
        return new(employees);
    }

    private async Task<Guid[]> EligibleBranchUserIdsAsync(DeviceRequestContext device, CancellationToken token)
    {
        Guid[] all = await organization.UserBranchAccesses.AsNoTracking().Where(access => access.OrganizationId == device.OrganizationId && access.Scope == BranchAccessScope.AllOrganizationBranches).Select(access => access.UserId).ToArrayAsync(token);
        Guid[] assigned = await (
            from access in organization.UserBranchAccesses.AsNoTracking()
            join assignment in organization.UserBranchAssignments.AsNoTracking() on new { AccessId = access.Id, access.OrganizationId } equals new { assignment.AccessId, assignment.OrganizationId }
            where access.OrganizationId == device.OrganizationId && access.Scope == BranchAccessScope.AssignedBranches && assignment.BranchId == device.BranchId && assignment.RevokedAtUtc == null
            select access.UserId).ToArrayAsync(token);
        return all.Concat(assigned).Distinct().ToArray();
    }
}
