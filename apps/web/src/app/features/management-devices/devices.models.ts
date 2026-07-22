export type DeviceStatus = "pendingPairing" | "active" | "revoked";
export interface DeviceSummary {
  id: string;
  branchId: string;
  branchName: string;
  name: string;
  type: string;
  platform: string | null;
  status: DeviceStatus;
  pairedAtUtc: string | null;
  lastSeenAtUtc: string | null;
  updatedAtUtc: string;
}
export interface DeviceDetail extends DeviceSummary {
  createdAtUtc: string;
}
export interface DeviceListResponse {
  items: DeviceSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface DeviceBranchOption {
  id: string;
  name: string;
  code: string;
}
export interface DeviceTypeOption {
  code: string;
  nameEn: string;
  nameAr: string;
}
export interface DeviceOptions {
  branches: DeviceBranchOption[];
  types: DeviceTypeOption[];
}
export interface DeviceWriteRequest {
  branchId: string;
  name: string;
  type: string;
  platform: string | null;
}
export interface VersionedDevice {
  device: DeviceDetail;
  etag: string;
}
export interface PairingChallenge {
  deviceId: string;
  pairingChallenge: string;
  expiresAtUtc: string;
}
export interface DeviceProblem {
  code?: string;
  currentEtag?: string;
}
