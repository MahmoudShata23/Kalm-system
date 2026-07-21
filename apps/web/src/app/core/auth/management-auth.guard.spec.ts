import { TestBed } from "@angular/core/testing";
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot } from "@angular/router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ManagementAuthService } from "./management-auth.service";
import { accessDeniedGuard, loginGuard, managementGuard } from "./management-auth.guard";

describe("management authorization guards", () => {
  const createUrlTree = vi.fn((commands: string[]) => ({ redirect: commands[0] }));
  const auth = {
    ensureInitialized: vi.fn(async () => undefined),
    user: vi.fn(() => ({ isAuthenticated: false })),
    hasPermission: vi.fn(() => false)
  };

  beforeEach(() => {
    vi.clearAllMocks();
    auth.user.mockReturnValue({ isAuthenticated: false });
    auth.hasPermission.mockReturnValue(false);
    TestBed.configureTestingModule({
      providers: [
        { provide: ManagementAuthService, useValue: auth },
        { provide: Router, useValue: { createUrlTree } }
      ]
    });
  });

  it("waits for initialization and redirects anonymous management navigation to login", async () => {
    const result = await TestBed.runInInjectionContext(() => managementGuard(
      {} as ActivatedRouteSnapshot,
      { url: "/management" } as RouterStateSnapshot));

    expect(auth.ensureInitialized).toHaveBeenCalledOnce();
    expect(createUrlTree).toHaveBeenCalledWith(["/management/login"], { queryParams: { returnUrl: "/management" } });
    expect(result).toEqual({ redirect: "/management/login" });
  });

  it("sends an authenticated user without management.access to access denied", async () => {
    auth.user.mockReturnValue({ isAuthenticated: true });
    const result = await TestBed.runInInjectionContext(() => managementGuard(
      {} as ActivatedRouteSnapshot,
      { url: "/management" } as RouterStateSnapshot));

    expect(result).toEqual({ redirect: "/management/access-denied" });
  });

  it("allows only an authenticated user with management.access", async () => {
    auth.user.mockReturnValue({ isAuthenticated: true });
    auth.hasPermission.mockReturnValue(true);
    const result = await TestBed.runInInjectionContext(() => managementGuard(
      {} as ActivatedRouteSnapshot,
      { url: "/management" } as RouterStateSnapshot));

    expect(result).toBe(true);
  });

  it("redirects authenticated login navigation by authorization and protects access denied from anonymous users", async () => {
    auth.user.mockReturnValue({ isAuthenticated: true });
    const loginResult = await TestBed.runInInjectionContext(() => loginGuard(
      {} as ActivatedRouteSnapshot,
      { url: "/management/login" } as RouterStateSnapshot));
    expect(loginResult).toEqual({ redirect: "/management/access-denied" });

    auth.user.mockReturnValue({ isAuthenticated: false });
    const deniedResult = await TestBed.runInInjectionContext(() => accessDeniedGuard(
      {} as ActivatedRouteSnapshot,
      { url: "/management/access-denied" } as RouterStateSnapshot));
    expect(deniedResult).toEqual({ redirect: "/management/login" });
  });
});
