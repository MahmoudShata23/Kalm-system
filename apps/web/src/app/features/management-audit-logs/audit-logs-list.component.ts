import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from "@angular/core";
import { DatePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { LanguageService } from "../../core/i18n/language.service";
import { AuditLogsFacade } from "./audit-logs.facade";
import { AuditLogFilter } from "./audit-logs.models";
import { AUDIT_COPY, AuditLanguage, auditActionLabel, auditResultLabel } from "./management-audit-logs.copy";

@Component({
  selector: "kalm-audit-logs-list",
  standalone: true,
  imports: [ButtonModule, DatePipe, FormsModule, RouterLink],
  providers: [AuditLogsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="audit" aria-labelledby="audit-heading">
      <header><p class="eyebrow">Milestone 1A</p><h2 id="audit-heading">{{ copy().heading }}</h2><p>{{ copy().intro }}</p></header>
      <form class="filters" aria-labelledby="filter-heading" (ngSubmit)="apply()">
        <h3 id="filter-heading">{{ copy().filters }}</h3>
        <label>{{ copy().from }}<input type="datetime-local" name="from" [(ngModel)]="fromLocal" required /></label>
        <label>{{ copy().to }}<input type="datetime-local" name="to" [(ngModel)]="toLocal" required /></label>
        <label>{{ copy().action }}<select name="action" [(ngModel)]="draft.action"><option value="">{{ copy().all }}</option>@for (option of facade.options().actions; track option.code) {<option [value]="option.code">{{ actionLabel(option.code) }}</option>}</select></label>
        <label>{{ copy().result }}<select name="result" [(ngModel)]="draft.result"><option value="">{{ copy().all }}</option>@for (option of facade.options().results; track option.code) {<option [value]="option.code">{{ resultLabel(option.code) }}</option>}</select></label>
        <label>{{ copy().branch }}<select name="branch" [(ngModel)]="draft.branchId"><option value="">{{ copy().all }}</option>@for (branch of facade.options().branches; track branch.id) {<option [value]="branch.id">{{ branch.code }} — {{ branch.name }}</option>}</select></label>
        <label>{{ copy().actorId }}<input name="actorId" [(ngModel)]="draft.actorId" autocomplete="off" /></label>
        <label>{{ copy().targetType }}<input name="targetType" [(ngModel)]="draft.targetType" autocomplete="off" /></label>
        <label>{{ copy().targetId }}<input name="targetId" [(ngModel)]="draft.targetId" autocomplete="off" /></label>
        <label>{{ copy().correlationId }}<input name="correlationId" [(ngModel)]="draft.correlationId" autocomplete="off" /></label>
        <label>{{ copy().pageSize }}<select name="pageSize" [(ngModel)]="draft.pageSize"><option [ngValue]="25">25</option><option [ngValue]="50">50</option><option [ngValue]="100">100</option></select></label>
        <p-button type="submit" icon="pi pi-filter" [label]="copy().apply" [loading]="facade.loading()" />
      </form>

      <div aria-live="polite">
        @if (facade.loading()) { <p class="state">{{ copy().loading }}</p> }
        @else if (facade.error()) { <section class="state error"><h3>{{ copy().error }}</h3><p-button type="button" [label]="copy().retry" (onClick)="facade.retry()" /></section> }
        @else if (facade.items().length === 0) { <p class="state">{{ copy().empty }}</p> }
        @else {
          <div class="table-wrap"><table><thead><tr><th>{{ copy().occurred }}</th><th>{{ copy().action }}</th><th>{{ copy().result }}</th><th>{{ copy().actor }}</th><th>{{ copy().target }}</th><th>{{ copy().branch }}</th><th>{{ copy().summary }}</th><th></th></tr></thead>
          <tbody>@for (item of facade.items(); track item.id) {<tr><td>{{ item.occurredAtUtc | date:'medium' }}</td><td>{{ actionLabel(item.action) }}</td><td>{{ resultLabel(item.result) }}</td><td>{{ item.actorDisplayName ?? copy().unknownActor }}</td><td>{{ targetLabel(item.targetType) }} @if (item.targetId) {<code>{{ item.targetId }}</code>}</td><td>{{ item.branch ? item.branch.code + ' — ' + item.branch.name : copy().noBranch }}</td><td>{{ actionLabel(item.action) }} · {{ targetLabel(item.targetType) }}</td><td><a class="open" [routerLink]="['/management/audit-logs', item.id]">{{ copy().open }}</a></td></tr>}</tbody></table></div>
          <nav class="paging" [attr.aria-label]="copy().heading"><p-button type="button" icon="pi pi-chevron-left" [label]="copy().previous" [disabled]="!facade.canPrevious()" (onClick)="facade.previous()" /><p-button type="button" icon="pi pi-chevron-right" iconPos="right" [label]="copy().next" [disabled]="!facade.canNext()" (onClick)="facade.next()" /></nav>
        }
      </div>
    </section>`,
  styles: [`
    .audit{display:grid;gap:1rem}.eyebrow{color:var(--p-primary-color);font-weight:700}.filters{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:.8rem;padding:1rem;border:1px solid var(--p-content-border-color);border-radius:var(--p-border-radius-md)}.filters h3{grid-column:1/-1;margin:0}label{display:grid;gap:.35rem;font-weight:600}input,select{min-height:44px;padding:.6rem;border:1px solid var(--p-form-field-border-color);border-radius:var(--p-border-radius-sm);background:var(--p-form-field-background);color:var(--p-form-field-color)}input:focus-visible,select:focus-visible,.open:focus-visible{outline:3px solid var(--p-focus-ring-color);outline-offset:2px}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;min-width:900px}th,td{text-align:start;padding:.75rem;border-bottom:1px solid var(--p-content-border-color);vertical-align:top}code{font-size:.75rem;overflow-wrap:anywhere}.open{display:inline-flex;align-items:center;min-height:44px}.state{padding:1rem;border-radius:var(--p-border-radius-md);background:var(--p-content-background)}.error{border-inline-start:4px solid var(--p-red-500)}.paging{display:flex;justify-content:space-between;gap:1rem;margin-top:1rem}@media(max-width:640px){.filters{grid-template-columns:1fr}}
  `]
})
export class AuditLogsListComponent implements OnInit {
  protected readonly facade = inject(AuditLogsFacade);
  private readonly language = inject(LanguageService);
  protected readonly copy = computed(() => AUDIT_COPY[this.language.language()]);
  protected draft: AuditLogFilter = { ...this.facade.filter() };
  protected fromLocal = this.local(this.draft.fromUtc);
  protected toLocal = this.local(this.draft.toUtc);

  async ngOnInit(): Promise<void> { await this.facade.initialize(); }
  protected async apply(): Promise<void> {
    const from = new Date(this.fromLocal); const to = new Date(this.toLocal);
    if (Number.isNaN(from.valueOf()) || Number.isNaN(to.valueOf())) return;
    await this.facade.apply({ ...this.draft, fromUtc: from.toISOString(), toUtc: to.toISOString(), actorId: this.draft.actorId.trim(), targetType: this.draft.targetType.trim(), targetId: this.draft.targetId.trim(), correlationId: this.draft.correlationId.trim() });
  }
  protected actionLabel(code: string): string { return auditActionLabel(this.language.language() as AuditLanguage, code); }
  protected resultLabel(code: string): string { return auditResultLabel(this.language.language() as AuditLanguage, code); }
  protected targetLabel(code: string): string {
    const labels: Record<string, readonly [string, string]> = { Authorization: ["Authorization", "التفويض"], Branch: ["Branch", "الفرع"], Device: ["Device", "الجهاز"], Organization: ["Organization", "المؤسسة"], Role: ["Role", "الدور"], User: ["User", "المستخدم"], Unknown: ["Unknown", "غير معروف"] };
    return labels[code]?.[this.language.language() === "ar" ? 1 : 0] ?? code;
  }
  private local(iso: string): string {
    const date = new Date(iso); const offset = date.getTimezoneOffset() * 60_000;
    return new Date(date.getTime() - offset).toISOString().slice(0, 16);
  }
}
