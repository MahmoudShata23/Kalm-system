import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { DeviceAuthApi } from "./device-auth.api";

describe("DeviceAuthApi", () => {
  let api: DeviceAuthApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(DeviceAuthApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("pairs without receiving a credential in JSON", () => {
    api
      .pair("device-id", "one-time-value")
      .subscribe((result) => expect(result).toBeNull());
    const request = http.expectOne("/api/v1/devices/pair");
    expect(request.request.method).toBe("POST");
    expect(request.request.body).toEqual({
      deviceId: "device-id",
      pairingChallenge: "one-time-value",
    });
    request.flush(null, { status: 204, statusText: "No Content" });
  });

  it("uses the explicit target user and PIN for device-bound login", () => {
    api.pinLogin("user-id", "123456").subscribe((response) => {
      expect(response).toEqual({
        displayName: "Employee",
        preferredLanguage: "en",
      });
      expect(Object.keys(response)).toEqual([
        "displayName",
        "preferredLanguage",
      ]);
    });
    const request = http.expectOne("/api/v1/auth/pin-login");
    expect(request.request.body).toEqual({ userId: "user-id", pin: "123456" });
    request.flush({ displayName: "Employee", preferredLanguage: "en" });
  });
});
