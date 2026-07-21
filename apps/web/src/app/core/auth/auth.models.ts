export interface LoginRequest {
  identifier: string;
  password: string;
}

export interface CurrentUser {
  isAuthenticated: boolean;
  username: string | null;
  displayName: string | null;
  preferredLanguage: "en" | "ar" | null;
  inactivityExpiresAtUtc: string | null;
  absoluteExpiresAtUtc: string | null;
  reauthenticationValidUntilUtc: string | null;
  permissions: string[];
  branchAccess: BranchAccess | null;
}

export interface BranchAccess {
  scope: "assignedBranches" | "allOrganizationBranches";
  branchIds: string[];
  operationalBranchIds: string[];
}

export interface CsrfTokenResponse {
  requestToken: string;
}
