import { HttpErrorResponse } from "@angular/common/http";
import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { DevicesApi } from "./devices.api";
import {
  DeviceListResponse,
  DeviceOptions,
  DeviceProblem,
  DeviceWriteRequest,
  PairingChallenge,
  VersionedDevice,
} from "./devices.models";

@Injectable({ providedIn: "root" })
export class DevicesFacade {
  private readonly api = inject(DevicesApi);
  private readonly listState = signal<DeviceListResponse | null>(null);
  private readonly detailState = signal<VersionedDevice | null>(null);
  private readonly optionsState = signal<DeviceOptions | null>(null);
  private readonly challengeState = signal<PairingChallenge | null>(null);
  private challengeGeneration = 0;
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  readonly list = this.listState.asReadonly();
  readonly detail = this.detailState.asReadonly();
  readonly options = this.optionsState.asReadonly();
  readonly pairingChallenge = this.challengeState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly saving = this.savingState.asReadonly();
  readonly errorCode = this.errorState.asReadonly();
  async loadList(
    status: string,
    branch: string,
    search: string,
    page: number,
  ): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set(null);
    try {
      const [list, options] = await Promise.all([
        firstValueFrom(this.api.list(status, branch, search, page)),
        this.optionsState()
          ? Promise.resolve(this.optionsState()!)
          : firstValueFrom(this.api.options()),
      ]);
      this.listState.set(list);
      this.optionsState.set(options);
    } catch (e) {
      this.errorState.set(this.code(e));
    } finally {
      this.loadingState.set(false);
    }
  }
  async loadEditor(id?: string): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set(null);
    this.clearChallenge();
    try {
      const [options, detail] = await Promise.all([
        firstValueFrom(this.api.options()),
        id ? firstValueFrom(this.api.get(id)) : Promise.resolve(null),
      ]);
      this.optionsState.set(options);
      this.detailState.set(detail);
    } catch (e) {
      this.errorState.set(this.code(e));
    } finally {
      this.loadingState.set(false);
    }
  }
  async save(value: DeviceWriteRequest): Promise<VersionedDevice | null> {
    const current = this.detailState();
    this.savingState.set(true);
    this.errorState.set(null);
    try {
      const result = await firstValueFrom(
        current
          ? this.api.update(current.device.id, current.etag, value)
          : this.api.create(value),
      );
      this.detailState.set(result);
      return result;
    } catch (e) {
      this.errorState.set(this.code(e));
      return null;
    } finally {
      this.savingState.set(false);
    }
  }
  async createChallenge(): Promise<void> {
    const current = this.detailState();
    if (!current) return;
    const generation = ++this.challengeGeneration;
    this.challengeState.set(null);
    const challenge = await firstValueFrom(
      this.api.challenge(current.device.id),
    );
    if (generation === this.challengeGeneration)
      this.challengeState.set(challenge);
  }
  async revoke(): Promise<boolean> {
    const current = this.detailState();
    if (!current) return false;
    try {
      const etag = await firstValueFrom(
        this.api.revoke(current.device.id, current.etag),
      );
      this.detailState.set({
        ...current,
        etag,
        device: { ...current.device, status: "revoked" },
      });
      this.clearChallenge();
      return true;
    } catch (e) {
      this.errorState.set(this.code(e));
      return false;
    }
  }
  clearChallenge(): void {
    this.challengeGeneration++;
    this.challengeState.set(null);
  }
  private code(error: unknown): string {
    return error instanceof HttpErrorResponse
      ? ((error.error as DeviceProblem | null)?.code ?? `http.${error.status}`)
      : "client.unexpected";
  }
}
