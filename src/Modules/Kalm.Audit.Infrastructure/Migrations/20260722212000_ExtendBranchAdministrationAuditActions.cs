using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalm.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendBranchAdministrationAuditActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_audit_logs_action", schema: "audit", table: "audit_logs");
            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_logs_action", schema: "audit", table: "audit_logs",
                sql: "action in ('OrganizationCreated', 'OrganizationUpdated', 'OrganizationStatusChanged', 'BranchCreated', 'BranchUpdated', 'BranchStatusChanged', 'BranchActivated', 'BranchDeactivated', 'BranchAdministrationRejected', 'OperationalBootstrapCompleted', 'PasswordCredentialActivated', 'ManagementLoginSucceeded', 'ManagementLoginFailed', 'ManagementAccountLocked', 'ManagementLogoutSucceeded', 'ManagementSessionRevoked', 'SystemRoleProvisioned', 'RoleCreated', 'RoleRenamed', 'RolePermissionSetChanged', 'RoleArchived', 'RoleAdministrationRejected', 'LastManagementAccessProtectionTriggered', 'UserCreated', 'UserProfileChanged', 'UserActivated', 'UserSuspended', 'UserPasswordSet', 'UserPasswordReset', 'UserAdministrationRejected', 'UserRoleAssigned', 'UserRoleRevoked', 'UserBranchAccessChanged', 'AuthorizationProvisioningCompleted', 'AuthorizationProvisioningFailed', 'ManagementAccessRevoked', 'AuthorizationSessionsRevoked', 'DeviceRegistered', 'DeviceUpdated', 'DevicePairingChallengeCreated', 'DevicePaired', 'DeviceCredentialRotated', 'DeviceRevoked', 'UserPinSet', 'UserPinReset', 'PinLoginSucceeded', 'PinLoginFailed', 'WorkstationLocked')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_audit_logs_action", schema: "audit", table: "audit_logs");
            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_logs_action", schema: "audit", table: "audit_logs",
                sql: "action in ('OrganizationCreated', 'OrganizationUpdated', 'OrganizationStatusChanged', 'BranchCreated', 'BranchUpdated', 'BranchStatusChanged', 'OperationalBootstrapCompleted', 'PasswordCredentialActivated', 'ManagementLoginSucceeded', 'ManagementLoginFailed', 'ManagementAccountLocked', 'ManagementLogoutSucceeded', 'ManagementSessionRevoked', 'SystemRoleProvisioned', 'RoleCreated', 'RoleRenamed', 'RolePermissionSetChanged', 'RoleArchived', 'RoleAdministrationRejected', 'LastManagementAccessProtectionTriggered', 'UserCreated', 'UserProfileChanged', 'UserActivated', 'UserSuspended', 'UserPasswordSet', 'UserPasswordReset', 'UserAdministrationRejected', 'UserRoleAssigned', 'UserRoleRevoked', 'UserBranchAccessChanged', 'AuthorizationProvisioningCompleted', 'AuthorizationProvisioningFailed', 'ManagementAccessRevoked', 'AuthorizationSessionsRevoked', 'DeviceRegistered', 'DeviceUpdated', 'DevicePairingChallengeCreated', 'DevicePaired', 'DeviceCredentialRotated', 'DeviceRevoked', 'UserPinSet', 'UserPinReset', 'PinLoginSucceeded', 'PinLoginFailed', 'WorkstationLocked')");
        }
    }
}
