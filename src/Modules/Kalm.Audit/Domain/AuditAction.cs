namespace Kalm.Audit.Domain;

public enum AuditAction
{
    OrganizationCreated,
    OrganizationUpdated,
    OrganizationStatusChanged,
    BranchCreated,
    BranchUpdated,
    BranchStatusChanged,
    OperationalBootstrapCompleted,
    PasswordCredentialActivated,
    ManagementLoginSucceeded,
    ManagementLoginFailed,
    ManagementAccountLocked,
    ManagementLogoutSucceeded,
    ManagementSessionRevoked,
    SystemRoleProvisioned,
    RoleCreated,
    RoleRenamed,
    RolePermissionSetChanged,
    RoleArchived,
    RoleAdministrationRejected,
    LastManagementAccessProtectionTriggered,
    UserRoleAssigned,
    UserRoleRevoked,
    UserBranchAccessChanged,
    AuthorizationProvisioningCompleted,
    AuthorizationProvisioningFailed,
    ManagementAccessRevoked,
    AuthorizationSessionsRevoked
}
