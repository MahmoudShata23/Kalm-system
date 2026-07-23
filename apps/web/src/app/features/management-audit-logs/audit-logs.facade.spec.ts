import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuditLogsApi } from "./audit-logs.api";
import { AuditLogsFacade, defaultAuditFilter } from "./audit-logs.facade";

describe("AuditLogsFacade", () => {
  const api = { options: vi.fn(), list: vi.fn(), get: vi.fn() };
  beforeEach(() => { vi.clearAllMocks(); TestBed.configureTestingModule({ providers: [AuditLogsFacade, { provide: AuditLogsApi, useValue: api }] }); });

  it("defaults to seven days and keeps cursors only in memory", async () => {
    const fixed = new Date("2026-07-23T12:00:00.000Z");
    const defaults = defaultAuditFilter(fixed);
    expect(Date.parse(defaults.toUtc) - Date.parse(defaults.fromUtc)).toBe(7 * 24 * 60 * 60 * 1000);
    expect(defaults.pageSize).toBe(25);
    api.options.mockReturnValue(of({ actions: [], results: [], branches: [] }));
    api.list.mockReturnValueOnce(of({ items: [], pageSize: 25, nextCursor: "next-secret", previousCursor: null }))
      .mockReturnValueOnce(of({ items: [], pageSize: 25, nextCursor: null, previousCursor: "previous-secret" }));
    const storage = vi.spyOn(Storage.prototype, "setItem");
    const facade = TestBed.inject(AuditLogsFacade);
    await facade.initialize(); expect(facade.canNext()).toBe(true);
    await facade.next(); expect(api.list.mock.calls[1][1]).toBe("next-secret");
    expect(storage).not.toHaveBeenCalled(); storage.mockRestore();
  });
});
