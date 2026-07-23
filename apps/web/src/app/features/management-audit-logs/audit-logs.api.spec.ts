import { provideHttpClient } from "@angular/common/http";
import { HttpTestingController, provideHttpClientTesting } from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { AuditLogsApi } from "./audit-logs.api";
import { AuditLogFilter } from "./audit-logs.models";

describe("AuditLogsApi", () => {
  let api: AuditLogsApi; let http: HttpTestingController;
  const filter: AuditLogFilter = { fromUtc: "2026-07-16T00:00:00.000Z", toUtc: "2026-07-23T00:00:00.000Z", action: "branchUpdated", result: "succeeded", actorId: "actor", targetType: "Branch", targetId: "target", branchId: "branch", correlationId: "corr", pageSize: 25 };
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }); api = TestBed.inject(AuditLogsApi); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());

  it("sends only the approved bounded filters and cursor", () => {
    api.list(filter, "protected-cursor").subscribe();
    const request = http.expectOne(candidate => candidate.url === "/api/v1/management/audit-logs");
    expect(request.request.method).toBe("GET");
    expect(request.request.params.keys().sort()).toEqual(["action", "actorId", "branchId", "correlationId", "cursor", "fromUtc", "pageSize", "result", "targetId", "targetType", "toUtc"]);
    expect(request.request.params.get("cursor")).toBe("protected-cursor");
    request.flush({ items: [], pageSize: 25, nextCursor: null, previousCursor: null });
  });
});
