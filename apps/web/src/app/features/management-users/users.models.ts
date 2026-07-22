export type UserStatus = "active" | "suspended" | "archived";
export type BranchAccessScope = "assignedBranches" | "allOrganizationBranches";

export interface UserSummary {
  id: string;
  username: string;
  email: string | null;
  displayName: string;
  preferredLanguage: "en" | "ar";
  status: UserStatus;
  roleNames: string[];
  updatedAtUtc: string;
}

export interface UserListResponse {
  items: UserSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface UserDetail {
  id: string;
  username: string;
  email: string | null;
  displayName: string;
  preferredLanguage: "en" | "ar";
  status: UserStatus;
  credentialStatus: "active" | "pendingSetup";
  roleIds: string[];
  branchAccessScope: BranchAccessScope;
  branchIds: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
  activatedAtUtc: string | null;
}

export interface VersionedUser {
  user: UserDetail;
  etag: string;
}

export interface UserRoleOption { id: string; name: string; }
export interface UserBranchOption { id: string; name: string; code: string; }
export interface UserEditorOptions { roles: UserRoleOption[]; branches: UserBranchOption[]; }

export interface UserWriteRequest {
  username: string;
  email: string | null;
  displayName: string;
  preferredLanguage: "en" | "ar";
  roleIds: string[];
  branchAccessScope: BranchAccessScope;
  branchIds: string[];
}

export interface UserCreateRequest extends UserWriteRequest { initialPassword: string | null; }

export interface UserProblem { code?: string; currentEtag?: string; }
