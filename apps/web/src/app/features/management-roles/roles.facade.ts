import { HttpErrorResponse } from "@angular/common/http";
import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { RolesApi } from "./roles.api";
import {
  PermissionCatalogueResponse,
  RoleDetail,
  RoleListResponse,
  RoleProblem,
  RoleWriteRequest,
  VersionedRole
} from "./roles.models";

@Injectable({ providedIn: "root" })
export class RolesFacade {
  private readonly api = inject(RolesApi);
  private readonly listState = signal<RoleListResponse | null>(null);
  private readonly detailState = signal<VersionedRole | null>(null);
  private readonly catalogueState = signal<PermissionCatalogueResponse | null>(null);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private readonly conflictState = signal<string | null>(null);
  private readonly announcementState = signal("");

  readonly list = this.listState.asReadonly();
  readonly detail = this.detailState.asReadonly();
  readonly catalogue = this.catalogueState.asReadonly();
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

  async loadEditor(roleId?: string): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set(null);
    this.conflictState.set(null);
    try {
      const cataloguePromise = this.catalogueState()
        ? Promise.resolve(this.catalogueState()!)
        : firstValueFrom(this.api.permissions());
      const [catalogue, detail] = await Promise.all([
        cataloguePromise,
        roleId ? firstValueFrom(this.api.get(roleId)) : Promise.resolve(null)
      ]);
      this.catalogueState.set(catalogue);
      this.detailState.set(detail);
    } catch (error) {
      this.errorState.set(this.problemCode(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async create(request: RoleWriteRequest): Promise<RoleDetail | null> {
    return this.save(async () => firstValueFrom(this.api.create(request)));
  }

  async update(request: RoleWriteRequest): Promise<RoleDetail | null> {
    const current = this.detailState();
    if (!current) return null;
    return this.save(async () => firstValueFrom(this.api.update(current.role.id, current.etag, request)));
  }

  async archive(): Promise<boolean> {
    const current = this.detailState();
    if (!current) return false;
    this.savingState.set(true);
    this.errorState.set(null);
    try {
      await firstValueFrom(this.api.archive(current.role.id, current.etag));
      this.announcementState.set("archived");
      return true;
    } catch (error) {
      this.captureMutationError(error);
      return false;
    } finally {
      this.savingState.set(false);
    }
  }

  private async save(operation: () => Promise<VersionedRole>): Promise<RoleDetail | null> {
    this.savingState.set(true);
    this.errorState.set(null);
    this.conflictState.set(null);
    try {
      const result = await operation();
      this.detailState.set(result);
      this.announcementState.set("saved");
      return result.role;
    } catch (error) {
      this.captureMutationError(error);
      return null;
    } finally {
      this.savingState.set(false);
    }
  }

  private captureMutationError(error: unknown): void {
    const code = this.problemCode(error);
    this.errorState.set(code);
    if (error instanceof HttpErrorResponse && error.status === 412) {
      const problem = error.error as RoleProblem | null;
      this.conflictState.set(problem?.currentEtag ?? "conflict");
    }
  }

  private problemCode(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const problem = error.error as RoleProblem | null;
      return problem?.code ?? `http.${error.status}`;
    }
    return "client.unexpected";
  }
}
