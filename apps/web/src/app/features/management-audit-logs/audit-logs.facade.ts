import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { AuditLogsApi } from "./audit-logs.api";
import { AuditLogDetail, AuditLogFilter, AuditLogItem, AuditViewerOptions } from "./audit-logs.models";

export function defaultAuditFilter(now = new Date()): AuditLogFilter {
  const from = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
  return { fromUtc: from.toISOString(), toUtc: now.toISOString(), action: "", result: "", actorId: "",
    targetType: "", targetId: "", branchId: "", correlationId: "", pageSize: 25 };
}

@Injectable()
export class AuditLogsFacade {
  private readonly api = inject(AuditLogsApi);
  private readonly itemsState = signal<AuditLogItem[]>([]);
  private readonly optionsState = signal<AuditViewerOptions>({ actions: [], results: [], branches: [] });
  private readonly filterState = signal<AuditLogFilter>(defaultAuditFilter());
  private readonly loadingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  private nextCursor: string | null = null;
  private previousCursor: string | null = null;

  readonly items = this.itemsState.asReadonly();
  readonly options = this.optionsState.asReadonly();
  readonly filter = this.filterState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly error = this.errorState.asReadonly();
  readonly canNext = signal(false);
  readonly canPrevious = signal(false);

  async initialize(): Promise<void> {
    try { this.optionsState.set(await firstValueFrom(this.api.options())); }
    catch { this.errorState.set("loadFailed"); }
    await this.load(null);
  }

  async apply(filter: AuditLogFilter): Promise<void> { this.filterState.set({ ...filter }); await this.load(null); }
  retry(): Promise<void> { return this.load(null); }
  next(): Promise<void> { return this.nextCursor ? this.load(this.nextCursor) : Promise.resolve(); }
  previous(): Promise<void> { return this.previousCursor ? this.load(this.previousCursor) : Promise.resolve(); }

  private async load(cursor: string | null): Promise<void> {
    if (this.loadingState()) return;
    this.loadingState.set(true); this.errorState.set(null);
    try {
      const response = await firstValueFrom(this.api.list(this.filterState(), cursor));
      this.itemsState.set(response.items); this.nextCursor = response.nextCursor; this.previousCursor = response.previousCursor;
      this.canNext.set(response.nextCursor !== null); this.canPrevious.set(response.previousCursor !== null);
    } catch { this.errorState.set("loadFailed"); }
    finally { this.loadingState.set(false); }
  }
}

@Injectable()
export class AuditLogDetailFacade {
  private readonly api = inject(AuditLogsApi);
  private readonly detailState = signal<AuditLogDetail | null>(null);
  private readonly loadingState = signal(false);
  private readonly errorState = signal<string | null>(null);
  readonly detail = this.detailState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly error = this.errorState.asReadonly();

  async load(id: string): Promise<void> {
    this.loadingState.set(true); this.errorState.set(null); this.detailState.set(null);
    try { this.detailState.set(await firstValueFrom(this.api.get(id))); }
    catch { this.errorState.set("loadFailed"); }
    finally { this.loadingState.set(false); }
  }
}
