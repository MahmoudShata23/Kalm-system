import { provideHttpClient } from "@angular/common/http";
import { HttpTestingController, provideHttpClientTesting } from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { BranchesApi } from "./branches.api";
import type { BranchDetail } from "./branches.models";

describe("BranchesApi", () => {
  let api: BranchesApi;
  let http: HttpTestingController;
  const branch: BranchDetail = {
    id: "11111111-1111-4111-8111-111111111111", name: "Cairo", code: "CAI", localeCode: "en",
    timeZoneId: "Africa/Cairo", businessDayRollover: "04:00", status: "active",
    createdAtUtc: "2026-07-22T00:00:00Z", updatedAtUtc: "2026-07-22T00:00:00Z"
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(BranchesApi);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it("uses bounded list parameters and strong ETags for every existing-branch mutation", () => {
    api.list("active", "Cairo", 2, 25).subscribe();
    const list = http.expectOne(request => request.url === "/api/v1/management/branches");
    expect(list.request.params.get("status")).toBe("active"); expect(list.request.params.get("search")).toBe("Cairo");
    expect(list.request.params.get("page")).toBe("2"); expect(list.request.params.get("pageSize")).toBe("25");
    list.flush({ items: [], page: 2, pageSize: 25, totalCount: 0 });

    const write = { name: branch.name, code: branch.code, localeCode: branch.localeCode, timeZoneId: branch.timeZoneId, businessDayRollover: branch.businessDayRollover };
    api.update(branch.id, '"3"', write).subscribe(result => expect(result.etag).toBe('"4"'));
    const update = http.expectOne(`/api/v1/management/branches/${branch.id}`); expect(update.request.headers.get("If-Match")).toBe('"3"');
    update.flush(branch, { headers: { ETag: '"4"' } });

    api.activate(branch.id, '"4"').subscribe();
    const activate = http.expectOne(`/api/v1/management/branches/${branch.id}/activate`); expect(activate.request.headers.get("If-Match")).toBe('"4"');
    activate.flush(branch, { headers: { ETag: '"5"' } });

    api.deactivate(branch.id, '"5"').subscribe();
    const deactivate = http.expectOne(`/api/v1/management/branches/${branch.id}/deactivate`); expect(deactivate.request.headers.get("If-Match")).toBe('"5"');
    deactivate.flush({ ...branch, status: "suspended" }, { headers: { ETag: '"6"' } });
  });
});
