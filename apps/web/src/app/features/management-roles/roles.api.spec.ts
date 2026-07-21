import { provideHttpClient } from "@angular/common/http";
import { HttpTestingController, provideHttpClientTesting } from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { RolesApi } from "./roles.api";
import type { RoleDetail } from "./roles.models";

describe("RolesApi", () => {
  let api: RolesApi;
  let http: HttpTestingController;
  const role: RoleDetail = {
    id: "11111111-1111-4111-8111-111111111111",
    name: "Manager",
    status: "active",
    isProtectedSystemRole: false,
    activeAssignmentCount: 0,
    permissionCodes: ["management.access"],
    createdAtUtc: "2026-07-21T00:00:00Z",
    updatedAtUtc: "2026-07-21T00:00:00Z",
    archivedAtUtc: null
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(RolesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("captures the authoritative ETag and sends it on complete replacement", () => {
    let receivedEtag = "";
    api.get(role.id).subscribe(result => receivedEtag = result.etag);
    http.expectOne(`/api/v1/management/roles/${role.id}`).flush(role, { headers: { ETag: "\"4\"" } });
    expect(receivedEtag).toBe("\"4\"");

    api.update(role.id, receivedEtag, { name: role.name, permissionCodes: role.permissionCodes }).subscribe();
    const update = http.expectOne(`/api/v1/management/roles/${role.id}`);
    expect(update.request.method).toBe("PUT");
    expect(update.request.headers.get("If-Match")).toBe("\"4\"");
    update.flush(role, { headers: { ETag: "\"5\"" } });
  });

  it("uses the dedicated archive action without exposing a delete request", () => {
    api.archive(role.id, "\"2\"").subscribe();
    const request = http.expectOne(`/api/v1/management/roles/${role.id}/archive`);
    expect(request.request.method).toBe("POST");
    expect(request.request.headers.get("If-Match")).toBe("\"2\"");
    request.flush(null);
  });
});
