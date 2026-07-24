import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from "@angular/core";
import { DatePipe } from "@angular/common";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { LanguageService } from "../../core/i18n/language.service";
import { AuditLogDetailFacade } from "./audit-logs.facade";
import { AuditSafeMetadata } from "./audit-logs.models";
import { AUDIT_COPY, AuditLanguage, auditActionLabel, auditResultLabel } from "./management-audit-logs.copy";

@Component({
  selector: "kalm-audit-log-detail",
  standalone: true,
  imports: [ButtonModule, DatePipe, RouterLink],
  providers: [AuditLogDetailFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="detail" aria-labelledby="detail-heading"><a class="back" routerLink="/management/audit-logs">← {{ copy().back }}</a><h2 id="detail-heading">{{ copy().detail }}</h2>
      <div aria-live="polite">@if (facade.loading()) {<p>{{ copy().loading }}</p>} @else if (facade.error()) {<section class="error"><h3>{{ copy().error }}</h3><p-button type="button" [label]="copy().retry" (onClick)="reload()" /></section>} @else if (facade.detail(); as detail) {
        <dl><dt>{{ copy().occurred }}</dt><dd>{{ detail.occurredAtUtc | date:'full' }}</dd><dt>{{ copy().action }}</dt><dd>{{ actionLabel(detail.action) }}</dd><dt>{{ copy().result }}</dt><dd>{{ resultLabel(detail.result) }}</dd><dt>{{ copy().actor }}</dt><dd>{{ detail.actorDisplayName ?? copy().unknownActor }} @if(detail.actorId){<code>{{ detail.actorId }}</code><button class="copy" type="button" (click)="copyText(detail.actorId)">{{ copy().copy }}</button>}</dd><dt>{{ copy().target }}</dt><dd>{{ detail.targetType }} @if(detail.targetId){<code>{{ detail.targetId }}</code><button class="copy" type="button" (click)="copyText(detail.targetId)">{{ copy().copy }}</button>}</dd><dt>{{ copy().branch }}</dt><dd>{{ detail.branch ? detail.branch.code + ' — ' + detail.branch.name : copy().noBranch }}</dd><dt>{{ copy().correlationId }}</dt><dd><code>{{ detail.correlationId }}</code><button class="copy" type="button" (click)="copyText(detail.correlationId)">{{ copy().copy }}</button></dd>@if(detail.reasonCode){<dt>{{ copy().reason }}</dt><dd>{{ detail.reasonCode }}</dd>}</dl>
        <section aria-labelledby="metadata-heading"><h3 id="metadata-heading">{{ copy().safeMetadata }}</h3>@if(detail.metadata; as metadata){<dl>@if(metadata.changedFields.length){<dt>{{ copy().changedFields }}</dt><dd>{{ metadata.changedFields.join(', ') }}</dd>}@if(metadata.previousStatus){<dt>{{ copy().previousStatus }}</dt><dd>{{ metadata.previousStatus }}</dd>}@if(metadata.newStatus){<dt>{{ copy().newStatus }}</dt><dd>{{ metadata.newStatus }}</dd>}@for(count of counts(metadata); track count.label){<dt>{{ count.label }}</dt><dd>{{ count.value }}</dd>}</dl>}@else{<p>{{ copy().noMetadata }}</p>}</section>
      }</div><p class="copied" aria-live="polite">{{ copied() ? copy().copied : '' }}</p>
    </section>`,
  styles: [`
    .detail{display:grid;gap:1rem;max-width:900px}.back,.copy{min-height:44px;display:inline-flex;align-items:center}.back:focus-visible,.copy:focus-visible{outline:3px solid var(--p-focus-ring-color);outline-offset:2px}dl{display:grid;grid-template-columns:minmax(150px,220px) 1fr;gap:.5rem 1rem;padding:1rem;border:1px solid var(--p-content-border-color);border-radius:var(--p-border-radius-md)}dt{font-weight:700}dd{margin:0;overflow-wrap:anywhere}.copy{margin-inline-start:.5rem;border:0;background:transparent;color:var(--p-primary-color);cursor:pointer}.error{border-inline-start:4px solid var(--p-red-500);padding:1rem}.copied{min-height:1.5rem}@media(max-width:560px){dl{grid-template-columns:1fr}dd{margin-bottom:.5rem}}
  `]
})
export class AuditLogDetailComponent implements OnInit {
  protected readonly facade = inject(AuditLogDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly language = inject(LanguageService);
  protected readonly copy = computed(() => AUDIT_COPY[this.language.language()]);
  protected readonly copied = signal(false);
  private id = "";
  async ngOnInit(): Promise<void> { this.id = this.route.snapshot.paramMap.get("auditLogId") ?? ""; await this.reload(); }
  protected reload(): Promise<void> { return this.facade.load(this.id); }
  protected actionLabel(code: string): string { return auditActionLabel(this.language.language() as AuditLanguage, code); }
  protected resultLabel(code: string): string { return auditResultLabel(this.language.language() as AuditLanguage, code); }
  protected counts(metadata: AuditSafeMetadata): { label: string; value: number }[] {
    const names: [keyof AuditSafeMetadata, string, string][] = [["registeredDeviceCount", "Registered devices", "الأجهزة المسجلة"], ["activeDeviceCount", "Active devices", "الأجهزة النشطة"], ["activeCredentialCount", "Active credentials", "بيانات الاعتماد النشطة"], ["activeSessionCount", "Active sessions", "الجلسات النشطة"], ["activeUserAssignmentCount", "Active user assignments", "تعيينات المستخدمين النشطة"], ["activeRoleAssignmentCount", "Active role assignments", "تعيينات الأدوار النشطة"], ["sessionsRevokedCount", "Sessions revoked", "الجلسات الملغاة"], ["affectedCount", "Affected records", "السجلات المتأثرة"]];
    return names.flatMap(([key, english, arabic]) => typeof metadata[key] === "number" ? [{ label: this.language.language() === "ar" ? arabic : english, value: metadata[key] as number }] : []);
  }
  protected async copyText(value: string): Promise<void> { await navigator.clipboard.writeText(value); this.copied.set(true); setTimeout(() => this.copied.set(false), 1500); }
}
