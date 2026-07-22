import { HttpErrorResponse } from "@angular/common/http";
import { TestBed } from "@angular/core/testing";
import { of, throwError } from "rxjs";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { BranchesApi } from "./branches.api";
import { BranchesFacade } from "./branches.facade";
import type { VersionedBranch } from "./branches.models";

describe("BranchesFacade", () => {
  const current: VersionedBranch = { branch: {
    id: "11111111-1111-4111-8111-111111111111", name: "Cairo", code: "CAI", localeCode: "en",
    timeZoneId: "Africa/Cairo", businessDayRollover: "04:00", status: "active",
    createdAtUtc: "2026-07-22T00:00:00Z", updatedAtUtc: "2026-07-22T00:00:00Z"
  }, etag: '"3"' };
  const api = {
    get: vi.fn(), list: vi.fn(), create: vi.fn(), update: vi.fn(), activate: vi.fn(), deactivate: vi.fn()
  };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({ providers: [BranchesFacade, { provide: BranchesApi, useValue: api }] });
  });

  it("keeps safe dependency counts and can refresh only the authoritative version", async () => {
    api.get.mockReturnValueOnce(of(current));
    const facade = TestBed.inject(BranchesFacade);
    await facade.load(current.branch.id);
    api.deactivate.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: {
      code: "branch.dependencies_active", dependencyCounts: {
        registeredDeviceCount: 0, activeDeviceCount: 1, activeCredentialCount: 1, activeSessionCount: 1, activeUserAssignmentCount: 2
      }
    } })));
    expect(await facade.deactivate()).toBe(false);
    expect(facade.errorCode()).toBe("branch.dependencies_active");
    expect(facade.dependencyCounts()?.activeUserAssignmentCount).toBe(2);

    api.get.mockReturnValueOnce(of({ ...current, etag: '"4"' }));
    await facade.refreshVersionPreservingDraft();
    expect(facade.detail()?.etag).toBe('"4"');
    expect(facade.errorCode()).toBeNull();
  });
});
