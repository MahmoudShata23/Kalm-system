using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kalm.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAuthorizationAuditActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_logs_action",
                schema: "audit",
                table: "audit_logs");
            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_logs_action",
                schema: "audit",
                table: "audit_logs",
                sql: "action in ('OrganizationCreated', 'OrganizationUpdated', 'OrganizationStatusChanged', 'BranchCreated', 'BranchUpdated', 'BranchStatusChanged', 'OperationalBootstrapCompleted', 'PasswordCredentialActivated', 'ManagementLoginSucceeded', 'ManagementLoginFailed', 'ManagementAccountLocked', 'ManagementLogoutSucceeded', 'ManagementSessionRevoked', 'SystemRoleProvisioned', 'RolePermissionSetChanged', 'UserRoleAssigned', 'UserRoleRevoked', 'UserBranchAccessChanged', 'AuthorizationProvisioningCompleted', 'AuthorizationProvisioningFailed', 'ManagementAccessRevoked', 'AuthorizationSessionsRevoked')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_logs_action",
                schema: "audit",
                table: "audit_logs");
            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_logs_action",
                schema: "audit",
                table: "audit_logs",
                sql: "action in ('OrganizationCreated', 'OrganizationUpdated', 'OrganizationStatusChanged', 'BranchCreated', 'BranchUpdated', 'BranchStatusChanged', 'OperationalBootstrapCompleted', 'PasswordCredentialActivated', 'ManagementLoginSucceeded', 'ManagementLoginFailed', 'ManagementAccountLocked', 'ManagementLogoutSucceeded', 'ManagementSessionRevoked')");
        }
    }
}
