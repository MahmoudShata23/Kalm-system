import { HttpErrorResponse } from "@angular/common/http";
import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { UsersApi } from "./users.api";
import {
  UserCreateRequest,
  UserDetail,
  UserEditorOptions,
  UserListResponse,
  UserProblem,
  UserWriteRequest,
  VersionedUser
} from "./users.models";

@Injectable({ providedIn: "root" })
export class UsersFacade {
  private readonly api = inject(UsersApi);
  private readonly listState = signal<UserListResponse | null>(null);
  private readonly detailState = signal<VersionedUser | null>(null);
  private readonly optionsState = signal<UserEditorOptions | null>(null);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private readonly conflictState = signal<string | null>(null);
  private readonly announcementState = signal("");

  readonly list = this.listState.asReadonly();
  readonly detail = this.detailState.asReadonly();
  readonly options = this.optionsState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly saving = this.savingState.asReadonly();
  readonly errorCode = this.errorState.asReadonly();
  readonly conflictEtag = this.conflictState.asReadonly();
  readonly announcement = this.announcementState.asReadonly();

  async loadList(status: string, search: string, page: number, pageSize: number): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set(null);
    try {
      this.listState.set(await firstValueFrom(this.api.list(status, search, page, pageSize)));
    } catch (error) {
      this.errorState.set(this.problemCode(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async loadEditor(userId?: string): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set(null);
    this.conflictState.set(null);
    try {
      const optionsPromise = this.optionsState() ? Promise.resolve(this.optionsState()!) : firstValueFrom(this.api.options());
      const [options, detail] = await Promise.all([
        optionsPromise,
        userId ? firstValueFrom(this.api.get(userId)) : Promise.resolve(null)
      ]);
      this.optionsState.set(options);
      this.detailState.set(detail);
    } catch (error) {
      this.errorState.set(this.problemCode(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  create(request: UserCreateRequest): Promise<UserDetail | null> {
    return this.save(() => firstValueFrom(this.api.create(request)), "saved");
  }

  update(request: UserWriteRequest): Promise<UserDetail | null> {
    const current = this.detailState();
    if (!current) return Promise.resolve(null);
    return this.save(() => firstValueFrom(this.api.update(current.user.id, current.etag, request)), "saved");
  }

  activate(): Promise<UserDetail | null> {
    const current = this.detailState();
    if (!current) return Promise.resolve(null);
    return this.save(() => firstValueFrom(this.api.activate(current.user.id, current.etag)), "activated");
  }

  suspend(): Promise<UserDetail | null> {
    const current = this.detailState();
    if (!current) return Promise.resolve(null);
    return this.save(() => firstValueFrom(this.api.suspend(current.user.id, current.etag)), "suspended");
  }

  async setPassword(password: string): Promise<UserDetail | null> {
    const current = this.detailState();
    if (!current) return Promise.resolve(null);
    this.savingState.set(true);
    this.errorState.set(null);
    this.conflictState.set(null);
    try {
      const etag = await firstValueFrom(this.api.setPassword(current.user.id, current.etag, password));
      const user = { ...current.user, credentialStatus: "active" as const };
      this.detailState.set({ user, etag });
      this.announcementState.set("passwordSet");
      return user;
    } catch (error) {
      this.captureMutationError(error);
      return null;
    } finally {
      this.savingState.set(false);
    }
  }

  private async save(operation: () => Promise<VersionedUser>, announcement: string): Promise<UserDetail | null> {
    this.savingState.set(true);
    this.errorState.set(null);
    this.conflictState.set(null);
    try {
      const result = await operation();
      this.detailState.set(result);
      this.announcementState.set(announcement);
      return result.user;
    } catch (error) {
      this.captureMutationError(error);
      return null;
    } finally {
      this.savingState.set(false);
    }
  }

  private captureMutationError(error: unknown): void {
    this.errorState.set(this.problemCode(error));
    if (error instanceof HttpErrorResponse && error.status === 412) {
      this.conflictState.set((error.error as UserProblem | null)?.currentEtag ?? "conflict");
    }
  }

  private problemCode(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return (error.error as UserProblem | null)?.code ?? `http.${error.status}`;
    }
    return "client.unexpected";
  }
}
