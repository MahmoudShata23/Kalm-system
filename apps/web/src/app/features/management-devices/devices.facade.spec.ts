import { TestBed } from "@angular/core/testing";
import { of, Subject } from "rxjs";
import { beforeEach, describe, expect, it } from "vitest";
import { DevicesApi } from "./devices.api";
import { DevicesFacade } from "./devices.facade";
import type { PairingChallenge, VersionedDevice } from "./devices.models";

describe("DevicesFacade", () => {
  const device: VersionedDevice = {
    etag: '"3"',
    device: {
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
    },
  };
  let challenge: Subject<PairingChallenge>;
  let facade: DevicesFacade;

  beforeEach(async () => {
    challenge = new Subject<PairingChallenge>();
    TestBed.configureTestingModule({
      providers: [
        DevicesFacade,
        {
          provide: DevicesApi,
          useValue: {
            options: () => of({ branches: [], types: [] }),
            get: () => of(device),
            challenge: () => challenge.asObservable(),
          },
        },
      ],
    });
    facade = TestBed.inject(DevicesFacade);
    await facade.loadEditor(device.device.id);
  });

  it("does not retain a challenge that arrives after navigation or destruction clears it", async () => {
    const pending = facade.createChallenge();
    facade.clearChallenge();
    challenge.next({
      deviceId: device.device.id,
      pairingChallenge: "one-time-secret",
      expiresAtUtc: "2026-07-22T00:10:00Z",
    });
    challenge.complete();
    await pending;
    expect(facade.pairingChallenge()).toBeNull();
  });
});
