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

    [Fact]
    public void PresentationCatalogue_CoversEveryPermissionExactlyOnceWithBilingualMetadata()
    {
        PermissionPresentation[] entries = PermissionPresentationCatalogue.All.ToArray();

        Assert.Equal(58, entries.Length);
        Assert.Equal(PermissionCatalogue.AllCodes.OrderBy(code => code), entries.Select(entry => entry.Code).OrderBy(code => code));
        Assert.Equal(entries.Length, entries.Select(entry => entry.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.GroupCode));
            Assert.True(entry.GroupOrder > 0);
            Assert.True(entry.ItemOrder > 0);
            Assert.False(string.IsNullOrWhiteSpace(entry.EnglishLabel));
            Assert.False(string.IsNullOrWhiteSpace(entry.EnglishDescription));
            Assert.False(string.IsNullOrWhiteSpace(entry.ArabicLabel));
            Assert.False(string.IsNullOrWhiteSpace(entry.ArabicDescription));
        });
        Assert.Equal(entries, entries.OrderBy(entry => entry.GroupOrder).ThenBy(entry => entry.ItemOrder));
    }

    [Fact]
    public void NormalRoleUpdate_ChangesNameAndPermissionSetWithOneVersionIncrementAndSupportsNoOp()
    {
        Role role = Role.Create(Guid.NewGuid(), Guid.NewGuid(), new RoleName("Manager"), null, Now);

        Assert.True(role.UpdateDefinition(new RoleName("Cafe Manager"), permissionSetChanged: true, Now.AddMinutes(1)));
        Assert.Equal(2, role.Version);
        Assert.Equal("Cafe Manager", role.Name);
        Assert.False(role.UpdateDefinition(new RoleName("Cafe Manager"), permissionSetChanged: false, Now.AddMinutes(2)));
        Assert.Equal(2, role.Version);
    }

    [Fact]
    public void ProtectedSystemRole_RejectsEveryNormalAdministrationMutation()
    {
        Role role = Role.Create(
            Guid.NewGuid(), Guid.NewGuid(), new RoleName("Initial Administrator"),
            PermissionCatalogue.FirstAdministratorSystemRoleKey, Now);

        Assert.True(role.IsProtectedSystemRole);
        Assert.Throws<InvalidOperationException>(() => role.UpdateDefinition(new RoleName("Renamed"), false, Now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => role.UpdateDefinition(new RoleName(role.Name), true, Now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => role.Archive(Now.AddMinutes(1)));

        role.RecordSystemPermissionSetProvisioned(Now.AddMinutes(1));
        Assert.Equal(2, role.Version);
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

    [Fact]
    public void UserAdministrationUpdate_AdvancesAggregateAndAuthorizationVersionsOnce()
    {
        Guid userId = Guid.NewGuid();
        var user = User.Create(userId, Guid.NewGuid(), new Username("employee"), null, new DisplayName("Employee"), "en", Now);

        bool changed = user.UpdateProfile(
            new Username("employee"),
            null,
            new DisplayName("Updated Employee"),
            "ar",
            authorizationChanged: true,
            Now.AddMinutes(1));

        Assert.True(changed);
        Assert.Equal(2, user.Version);
        Assert.Equal(2, user.AuthorizationVersion);
        Assert.Equal("Updated Employee", user.DisplayName);
        Assert.Equal("ar", user.PreferredLanguage);
    }

    [Fact]
    public void UserAdministrationNoOp_KeepsVersionsStable()
    {
        var user = User.Create(
            Guid.NewGuid(), Guid.NewGuid(), new Username("employee"), null, new DisplayName("Employee"), "en", Now);

        bool changed = user.UpdateProfile(
            new Username("employee"), null, new DisplayName("Employee"), "en",
            authorizationChanged: false, Now.AddMinutes(1));

        Assert.False(changed);
        Assert.Equal(1, user.Version);
        Assert.Equal(1, user.AuthorizationVersion);
    }
}
