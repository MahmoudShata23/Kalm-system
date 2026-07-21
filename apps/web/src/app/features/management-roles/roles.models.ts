export type RoleStatus = "active" | "archived";

export interface RoleSummary {
  id: string;
  name: string;
  status: RoleStatus;
  isProtectedSystemRole: boolean;
  permissionCount: number;
  activeAssignmentCount: number;
  updatedAtUtc: string;
}

export interface RoleListResponse {
  items: RoleSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface RoleDetail {
  id: string;
  name: string;
  status: RoleStatus;
  isProtectedSystemRole: boolean;
  activeAssignmentCount: number;
  permissionCodes: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
  archivedAtUtc: string | null;
}

export interface VersionedRole {
  role: RoleDetail;
  etag: string;
}

export interface RoleWriteRequest {
  name: string;
  permissionCodes: string[];
}

export interface PermissionPresentation {
  code: string;
  groupCode: string;
  groupOrder: number;
  itemOrder: number;
  englishLabel: string;
  englishDescription: string;
  arabicLabel: string;
  arabicDescription: string;
}

export interface PermissionCatalogueResponse {
  catalogueVersion: string;
  permissions: PermissionPresentation[];
}

export interface RoleProblem {
  code?: string;
  currentEtag?: string;
  activeAssignmentCount?: number;
}
