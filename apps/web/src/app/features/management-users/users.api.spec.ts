import { provideHttpClient } from "@angular/common/http";
import { HttpTestingController, provideHttpClientTesting } from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { UsersApi } from "./users.api";
import type { UserDetail } from "./users.models";

describe("UsersApi", () => {
  let api: UsersApi;
  let http: HttpTestingController;
  const user: UserDetail = {
    id: "22222222-2222-4222-8222-222222222222",
    username: "employee",
    email: null,
    displayName: "Employee",
    preferredLanguage: "en",
    status: "suspended",
    credentialStatus: "pendingSetup",
    roleIds: ["11111111-1111-4111-8111-111111111111"],
    branchAccessScope: "assignedBranches",
    branchIds: ["33333333-3333-4333-8333-333333333333"],
    createdAtUtc: "2026-07-22T00:00:00Z",
    updatedAtUtc: "2026-07-22T00:00:00Z",
    activatedAtUtc: null
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(UsersApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("captures and sends the authoritative user ETag", () => {
    let etag = "";
    api.get(user.id).subscribe(result => etag = result.etag);
    http.expectOne(`/api/v1/management/users/${user.id}`).flush(user, { headers: { ETag: "\"4\"" } });
    expect(etag).toBe("\"4\"");

    api.update(user.id, etag, {
      username: user.username,
      email: user.email,
      displayName: user.displayName,
      preferredLanguage: user.preferredLanguage,
      roleIds: user.roleIds,
      branchAccessScope: user.branchAccessScope,
      branchIds: user.branchIds
    }).subscribe();
    const update = http.expectOne(`/api/v1/management/users/${user.id}`);
    expect(update.request.method).toBe("PUT");
    expect(update.request.headers.get("If-Match")).toBe("\"4\"");
    update.flush(user, { headers: { ETag: "\"5\"" } });
  });

  it("accepts an empty password response and captures its authoritative ETag", () => {
    let etag = "";
    api.setPassword(user.id, "\"5\"", "temporary-password").subscribe(result => etag = result);
    const request = http.expectOne(`/api/v1/management/users/${user.id}/password`);
    expect(request.request.method).toBe("POST");
    expect(request.request.headers.get("If-Match")).toBe("\"5\"");
    expect(request.request.body).toEqual({ password: "temporary-password" });
    request.flush(null, { status: 204, statusText: "No Content", headers: { ETag: "\"6\"" } });
    expect(etag).toBe("\"6\"");
  });

  it("sends only the PIN and captures the authoritative ETag from a no-content response", () => {
    let etag = "";
    api.setPin(user.id, "\"6\"", "123456").subscribe(result => etag = result);
    const request = http.expectOne(`/api/v1/management/users/${user.id}/pin`);
    expect(request.request.method).toBe("POST");
    expect(request.request.headers.get("If-Match")).toBe("\"6\"");
    expect(request.request.body).toEqual({ pin: "123456" });
    request.flush(null, { status: 204, statusText: "No Content", headers: { ETag: "\"7\"" } });
    expect(etag).toBe("\"7\"");
  });
});
