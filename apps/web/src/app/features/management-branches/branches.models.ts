export type BranchStatus = "setup" | "active" | "suspended" | "archived";

export interface BranchSummary {
  id: string;
  name: string;
  code: string;
  localeCode: string;
  timeZoneId: string;
  businessDayRollover: string;
  status: BranchStatus;
  updatedAtUtc: string;
}

export interface BranchDetail extends BranchSummary {
  createdAtUtc: string;
}

export interface BranchListResponse {
  items: BranchSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface BranchWriteRequest {
  name: string;
  code: string;
  localeCode: string;
  timeZoneId: string;
  businessDayRollover: string;
}

export interface VersionedBranch {
  branch: BranchDetail;
  etag: string;
}

export interface BranchDependencyCounts {
  registeredDeviceCount: number;
  activeDeviceCount: number;
  activeCredentialCount: number;
  activeSessionCount: number;
  activeUserAssignmentCount: number;
}

export interface BranchProblem {
  code?: string;
  currentEtag?: string;
  dependencyCounts?: BranchDependencyCounts;
}
