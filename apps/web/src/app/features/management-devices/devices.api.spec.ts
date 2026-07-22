import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { DevicesApi } from "./devices.api";
import type { DeviceDetail } from "./devices.models";

describe("DevicesApi", () => {
  let api: DevicesApi;
  let http: HttpTestingController;
  const device: DeviceDetail = {
    id: "11111111-1111-4111-8111-111111111111",
    branchId: "22222222-2222-4222-8222-222222222222",
    branchName: "Cairo",
    name: "Counter POS",
    type: "posTerminal",
    platform: "Windows",
    status: "active",
    pairedAtUtc: "2026-07-22T00:00:00Z",
    lastSeenAtUtc: null,
    createdAtUtc: "2026-07-22T00:00:00Z",
    updatedAtUtc: "2026-07-22T00:00:00Z",
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(DevicesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("uses strong ETags for update and revocation", () => {
    api
      .update(device.id, '"3"', {
        branchId: device.branchId,
        name: device.name,
        type: device.type,
        platform: device.platform,
      })
      .subscribe();
    const update = http.expectOne(`/api/v1/management/devices/${device.id}`);
    expect(update.request.headers.get("If-Match")).toBe('"3"');
    update.flush(device, { headers: { ETag: '"4"' } });

    api.revoke(device.id, '"4"').subscribe((etag) => expect(etag).toBe('"5"'));
    const revoke = http.expectOne(
      `/api/v1/management/devices/${device.id}/revoke`,
    );
    expect(revoke.request.headers.get("If-Match")).toBe('"4"');
    revoke.flush(null, {
      status: 204,
      statusText: "No Content",
      headers: { ETag: '"5"' },
    });
  });
});
