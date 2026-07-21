using Kalm.Identity.Authorization;
using Kalm.Identity.Domain;
using Kalm.Identity.Domain.ValueObjects;
using Kalm.Organization.Domain;

namespace Kalm.UnitTests.Identity;

public sealed class AuthorizationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Catalogue_ContainsUniqueSortedStableCodesAndVersionedAdministratorSet()
    {
        Assert.Equal(PermissionCatalogue.AllCodes.OrderBy(code => code, StringComparer.Ordinal), PermissionCatalogue.AllCodes);
        Assert.Equal(PermissionCatalogue.AllCodes.Count, PermissionCatalogue.AllCodes.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(PermissionCodes.ManagementAccess, PermissionCatalogue.AllCodes);
        Assert.Equal(PermissionCatalogue.AllCodes, PermissionCatalogue.FirstAdministratorPermissionCodes);
        Assert.NotSame(PermissionCatalogue.AllCodes, PermissionCatalogue.FirstAdministratorPermissionCodes);
        Assert.Equal("2026.07.slice3.v1", PermissionCatalogue.FirstAdministratorPermissionSetVersion);
    }

    [Theory]
    [InlineData("Management.Access")]
    [InlineData("management")]
    [InlineData("management..access")]
    [InlineData("management-access")]
    [InlineData(" management.access ")]
    public void PermissionCode_RejectsMalformedOrNonCanonicalValues(string value)
    {
        Assert.Throws<ArgumentException>(() => new PermissionCode(value));
    }

    [Fact]
    public void UnknownAndRetiredPermissions_AreNotApprovedByCompiledCatalogue()
    {
        Assert.False(PermissionCatalogue.Contains("unknown.permission"));
        Permission permission = Permission.Create(Guid.NewGuid(), new PermissionCode(PermissionCodes.ManagementAccess), Now);
        permission.Retire(Now.AddMinutes(1));
        Assert.Equal(PermissionStatus.Retired, permission.Status);
    }

    [Fact]
    public void RoleGrantAndAssignment_HaveExplicitLifecycleIndependentOfRoleName()
    {
        Guid organizationId = Guid.NewGuid();
        Role first = Role.Create(Guid.NewGuid(), organizationId, new RoleName("Owner"), "system.one", Now);
        Role second = Role.Create(Guid.NewGuid(), organizationId, new RoleName("Unrelated display"), "system.two", Now);
        Guid permissionId = Guid.NewGuid();
        RolePermission firstGrant = RolePermission.Grant(Guid.NewGuid(), first.Id, permissionId, Now);
        RolePermission secondGrant = RolePermission.Grant(Guid.NewGuid(), second.Id, permissionId, Now);
        UserRoleAssignment assignment = UserRoleAssignment.Assign(Guid.NewGuid(), organizationId, Guid.NewGuid(), first.Id, Now);

        Assert.Equal(permissionId, firstGrant.PermissionId);
        Assert.Equal(permissionId, secondGrant.PermissionId);
        Assert.NotEqual(first.Name, second.Name);
        assignment.Revoke(Now.AddMinutes(1));
        firstGrant.Revoke(Now.AddMinutes(1));
        Assert.NotNull(assignment.RevokedAtUtc);
        Assert.NotNull(firstGrant.RevokedAtUtc);
    }

    [Fact]
    public void AuthorizationMutation_AdvancesUserAuthorizationAndAggregateVersions()
    {
        Guid userId = Guid.NewGuid();
        var user = User.Create(userId, Guid.NewGuid(), new Username("manager"), null, new DisplayName("Manager"), "en", Now);
        var credential = PasswordCredential.Create(Guid.NewGuid(), userId, Now);
        credential.CompleteSetup("$kalm$test", Now);
        user.Activate(credential, Now);
        long aggregateVersion = user.Version;

        user.AdvanceAuthorizationVersion(Now.AddMinutes(1));

        Assert.Equal(2, user.AuthorizationVersion);
        Assert.Equal(aggregateVersion + 1, user.Version);
    }

    [Fact]
    public void BranchAccess_RepresentsBothApprovedScopesAndVersionedAssignments()
    {
        Guid organizationId = Guid.NewGuid();
        UserBranchAccess access = UserBranchAccess.Create(
            Guid.NewGuid(), organizationId, Guid.NewGuid(), BranchAccessScope.AssignedBranches, Now);
        UserBranchAssignment assignment = UserBranchAssignment.Assign(
            Guid.NewGuid(), access.Id, organizationId, Guid.NewGuid(), Now);

        access.ChangeScope(BranchAccessScope.AllOrganizationBranches, Now.AddMinutes(1));
        assignment.Revoke(Now.AddMinutes(1));

        Assert.Equal(BranchAccessScope.AllOrganizationBranches, access.Scope);
        Assert.Equal(2, access.Version);
        Assert.Equal(2, assignment.Version);
    }
}
