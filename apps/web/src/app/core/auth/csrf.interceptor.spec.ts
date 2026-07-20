import { HttpClient, provideHttpClient, withInterceptors } from "@angular/common/http";
import { HttpTestingController, provideHttpClientTesting } from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { csrfInterceptor } from "./csrf.interceptor";
import { CsrfTokenStore } from "./csrf-token.store";

describe("csrfInterceptor", () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let store: CsrfTokenStore;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([csrfInterceptor])),
      provideHttpClientTesting()
    ] });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    store = TestBed.inject(CsrfTokenStore);
    store.replace("memory-only-token");
  });

  afterEach(() => controller.verify());

  it("attaches the in-memory token only to unsafe same-origin API requests", () => {
    http.post("/api/v1/auth/login", {}).subscribe();
    expect(controller.expectOne("/api/v1/auth/login").request.headers.get("X-XSRF-TOKEN")).toBe("memory-only-token");
    http.get("/api/v1/auth/me").subscribe();
    expect(controller.expectOne("/api/v1/auth/me").request.headers.has("X-XSRF-TOKEN")).toBe(false);
    http.post("https://example.test/api/v1/auth/login", {}).subscribe();
    expect(controller.expectOne("https://example.test/api/v1/auth/login").request.headers.has("X-XSRF-TOKEN")).toBe(false);
  });
});
