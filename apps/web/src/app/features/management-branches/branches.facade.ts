import { HttpErrorResponse } from "@angular/common/http";
import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { BranchesApi } from "./branches.api";
import { BranchDependencyCounts, BranchListResponse, BranchProblem, BranchWriteRequest, VersionedBranch } from "./branches.models";

@Injectable({ providedIn: "root" })
export class BranchesFacade {
  private readonly api = inject(BranchesApi);
  private readonly listState = signal<BranchListResponse | null>(null);
  private readonly detailState = signal<VersionedBranch | null>(null);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private readonly dependenciesState = signal<BranchDependencyCounts | null>(null);

  readonly list = this.listState.asReadonly();
  readonly detail = this.detailState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly saving = this.savingState.asReadonly();
  readonly errorCode = this.errorState.asReadonly();
  readonly dependencyCounts = this.dependenciesState.asReadonly();

  async loadList(status: string, search: string, page: number): Promise<void> {
    this.loadingState.set(true);
    this.clearError();
    try {
      this.listState.set(await firstValueFrom(this.api.list(status, search, page, 25)));
    } catch (error) {
      this.capture(error);
    } finally {
      this.loadingState.set(false);
    }
  }

  async load(branchId?: string): Promise<void> {
    this.loadingState.set(true);
    this.clearError();
    try {
      this.detailState.set(branchId ? await firstValueFrom(this.api.get(branchId)) : null);
    } catch (error) {
      this.capture(error);
    } finally {
      this.loadingState.set(false);
    }
  }

  async save(request: BranchWriteRequest): Promise<VersionedBranch | null> {
    const current = this.detailState();
    this.savingState.set(true);
    this.clearError();
    try {
      const result = await firstValueFrom(current
        ? this.api.update(current.branch.id, current.etag, request)
        : this.api.create(request));
      this.detailState.set(result);
      return result;
    } catch (error) {
      this.capture(error);
      return null;
    } finally {
      this.savingState.set(false);
    }
  }

  async activate(): Promise<boolean> {
    return this.changeStatus(true);
  }

  async deactivate(): Promise<boolean> {
    return this.changeStatus(false);
  }

  async refreshVersionPreservingDraft(): Promise<void> {
    const current = this.detailState();
    if (!current) return;
    try {
      this.detailState.set(await firstValueFrom(this.api.get(current.branch.id)));
      this.clearError();
    } catch (error) {
      this.capture(error);
    }
  }

  clearError(): void {
    this.errorState.set(null);
    this.dependenciesState.set(null);
  }

  private async changeStatus(activate: boolean): Promise<boolean> {
    const current = this.detailState();
    if (!current) return false;
    this.savingState.set(true);
    this.clearError();
    try {
      const result = await firstValueFrom(activate
        ? this.api.activate(current.branch.id, current.etag)
        : this.api.deactivate(current.branch.id, current.etag));
      this.detailState.set(result);
      return true;
    } catch (error) {
      this.capture(error);
      return false;
    } finally {
      this.savingState.set(false);
    }
  }

  private capture(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      const problem = error.error as BranchProblem | null;
      this.errorState.set(problem?.code ?? `http.${error.status}`);
      this.dependenciesState.set(problem?.dependencyCounts ?? null);
      return;
    }
    this.errorState.set("client.unexpected");
  }
}
