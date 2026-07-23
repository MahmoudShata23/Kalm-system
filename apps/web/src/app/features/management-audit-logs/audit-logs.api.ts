import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { AuditLogDetail, AuditLogFilter, AuditLogListResponse, AuditViewerOptions } from "./audit-logs.models";

@Injectable({ providedIn: "root" })
export class AuditLogsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = "/api/v1/management/audit-logs";

  list(filter: AuditLogFilter, cursor: string | null): Observable<AuditLogListResponse> {
    let params = new HttpParams()
      .set("fromUtc", filter.fromUtc).set("toUtc", filter.toUtc).set("pageSize", filter.pageSize);
    for (const [key, value] of Object.entries({
      action: filter.action, result: filter.result, actorId: filter.actorId,
      targetType: filter.targetType, targetId: filter.targetId,
      branchId: filter.branchId, correlationId: filter.correlationId
    })) if (value) params = params.set(key, value);
    if (cursor) params = params.set("cursor", cursor);
    return this.http.get<AuditLogListResponse>(this.baseUrl, { params });
  }

  options(): Observable<AuditViewerOptions> { return this.http.get<AuditViewerOptions>(`${this.baseUrl}/options`); }
  get(auditLogId: string): Observable<AuditLogDetail> { return this.http.get<AuditLogDetail>(`${this.baseUrl}/${auditLogId}`); }
}
