export interface AuditBranchHint { id: string; code: string; name: string; }
export interface AuditFilterOption { code: string; presentationKey: string; category: string; }
export interface AuditViewerOptions { actions: AuditFilterOption[]; results: AuditFilterOption[]; branches: AuditBranchHint[]; }
export interface AuditLogItem {
  id: string; occurredAtUtc: string; action: string; result: string; actorId: string | null;
  actorDisplayName: string | null; targetType: string; targetId: string | null;
  branch: AuditBranchHint | null; correlationId: string; summary: string;
}
export interface AuditSafeMetadata {
  changedFields: string[]; previousStatus: string | null; newStatus: string | null;
  registeredDeviceCount: number | null; activeDeviceCount: number | null;
  activeCredentialCount: number | null; activeSessionCount: number | null;
  activeUserAssignmentCount: number | null; activeRoleAssignmentCount: number | null;
  sessionsRevokedCount: number | null; affectedCount: number | null; relatedUserId: string | null;
  relatedBranchId: string | null; relatedDeviceId: string | null;
}
export interface AuditLogDetail extends AuditLogItem { reasonCode: string | null; metadata: AuditSafeMetadata | null; }
export interface AuditLogListResponse { items: AuditLogItem[]; pageSize: number; nextCursor: string | null; previousCursor: string | null; }
export interface AuditLogFilter {
  fromUtc: string; toUtc: string; action: string; result: string; actorId: string;
  targetType: string; targetId: string; branchId: string; correlationId: string; pageSize: number;
}
