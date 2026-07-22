import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { BRANCHES_MANAGE_PERMISSION } from "../../core/auth/management-permissions";
import { LanguageService } from "../../core/i18n/language.service";
import { BranchesFacade } from "./branches.facade";
import { BRANCHES_COPY } from "./management-branches.copy";

@Component({
  selector: "kalm-branch-editor",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonModule, InputTextModule],
  template: `
    <section class="editor" aria-labelledby="branch-heading">
      <a routerLink="/management/branches">← {{ copy().back }}</a>
      <h2 id="branch-heading">{{ isNew ? copy().createHeading : copy().editHeading }}</h2>
      @if (loading()) { <p aria-live="polite">{{ copy().loading }}</p> }
      @else if (error() && !detail()) { <div role="alert"><p>{{ copy().operationError }}</p><p-button type="button" [label]="copy().retry" (onClick)="load()" /></div> }
      @else {
        @if (detail()?.branch?.status === "archived") { <p role="status">{{ copy().archivedReadOnly }}</p> }
        <form [formGroup]="form" (ngSubmit)="save()">
          <label>{{ copy().name }}<input pInputText formControlName="name" /></label>
          <label>{{ copy().code }}<input pInputText formControlName="code" autocomplete="off" /></label>
          <label>{{ copy().locale }}<select formControlName="localeCode"><option value="en">{{ copy().english }}</option><option value="ar-EG">{{ copy().arabic }}</option></select></label>
          <label>{{ copy().timeZone }}<input pInputText formControlName="timeZoneId" autocomplete="off" /></label>
          <label>{{ copy().rollover }}<input type="time" formControlName="businessDayRollover" /></label>
          @if (canManage()) { <p-button type="submit" [label]="copy().save" [loading]="saving()" [disabled]="form.invalid || detail()?.branch?.status === 'archived'" /> }
        </form>
        @if (form.invalid && form.touched) { <p role="alert">{{ copy().invalid }}</p> }
        @if (detail(); as current) {
          @if (canManage() && current.branch.status !== "archived") {
            <div class="actions">
              @if (current.branch.status === "active") { <p-button type="button" severity="danger" [label]="copy().deactivate" (onClick)="confirmation.set('deactivate')" /> }
              @else { <p-button type="button" [label]="copy().activate" (onClick)="confirmation.set('activate')" /> }
            </div>
          }
        }
        @if (error() === "branch.concurrency_conflict") {
          <aside class="notice" role="alert"><h3>{{ copy().conflict }}</h3><p-button type="button" [label]="copy().refreshVersion" (onClick)="refreshVersion()" /></aside>
        } @else if (error() === "branch.dependencies_active" && dependencies(); as counts) {
          <aside class="notice" role="alert"><h3>{{ copy().dependencyTitle }}</h3><p>{{ copy().dependencyHelp }}</p>
            <ul><li>{{ copy().registeredDevices }}: {{ counts.registeredDeviceCount }}</li><li>{{ copy().activeDevices }}: {{ counts.activeDeviceCount }}</li>
              <li>{{ copy().activeCredentials }}: {{ counts.activeCredentialCount }}</li><li>{{ copy().activeSessions }}: {{ counts.activeSessionCount }}</li>
              <li>{{ copy().assignments }}: {{ counts.activeUserAssignmentCount }}</li></ul></aside>
        } @else if (error()) { <p role="alert">{{ copy().operationError }}</p> }
        @if (confirmation(); as action) {
          <div class="dialog" role="dialog" aria-modal="true" [attr.aria-label]="action === 'activate' ? copy().activate : copy().deactivate">
            <p>{{ action === "activate" ? copy().confirmActivate : copy().confirmDeactivate }}</p>
            <div class="actions"><p-button type="button" [severity]="action === 'deactivate' ? 'danger' : undefined" [label]="copy().confirm" (onClick)="changeStatus(action)" />
              <p-button type="button" severity="secondary" [label]="copy().cancel" (onClick)="confirmation.set(null)" /></div>
          </div>
        }
      }
    </section>`,
  styles: [`
    :host { display:block } .editor,form { display:grid; gap:1rem; max-width:48rem } label { display:grid; gap:.35rem } input,select,a { min-height:44px }
    .actions { display:flex; gap:.75rem; flex-wrap:wrap } .notice,.dialog { padding:1rem; border:1px solid var(--p-primary-300); border-radius:12px; background:var(--p-surface-50) }
    .dialog { position:relative } a:focus-visible,input:focus-visible,select:focus-visible { outline:3px solid var(--p-primary-500); outline-offset:2px }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BranchEditorComponent implements OnInit {
  private readonly facade = inject(BranchesFacade);
  private readonly language = inject(LanguageService);
  private readonly auth = inject(ManagementAuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly id = this.route.snapshot.paramMap.get("branchId") ?? undefined;
  protected readonly isNew = !this.id;
  protected readonly detail = this.facade.detail;
  protected readonly loading = this.facade.loading;
  protected readonly saving = this.facade.saving;
  protected readonly error = this.facade.errorCode;
  protected readonly dependencies = this.facade.dependencyCounts;
  protected readonly confirmation = signal<"activate" | "deactivate" | null>(null);
  protected readonly copy = computed(() => BRANCHES_COPY[this.language.language()]);
  protected readonly canManage = computed(() => this.auth.hasPermission(BRANCHES_MANAGE_PERMISSION));
  protected readonly form = new FormGroup({
    name: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    code: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[A-Za-z0-9-]{2,20}$/)] }),
    localeCode: new FormControl("en", { nonNullable: true, validators: [Validators.required] }),
    timeZoneId: new FormControl("Africa/Cairo", { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    businessDayRollover: new FormControl("04:00", { nonNullable: true, validators: [Validators.required, Validators.pattern(/^([01]\d|2[0-3]):[0-5]\d$/)] })
  });

  async ngOnInit(): Promise<void> { await this.load(); }
  protected async load(): Promise<void> {
    await this.facade.load(this.id);
    const branch = this.detail()?.branch;
    if (branch) this.form.reset({ name: branch.name, code: branch.code, localeCode: branch.localeCode, timeZoneId: branch.timeZoneId, businessDayRollover: branch.businessDayRollover });
    if (!this.canManage()) this.form.disable();
  }
  protected async save(): Promise<void> {
    if (!this.canManage() || this.form.invalid) { this.form.markAllAsTouched(); return; }
    const result = await this.facade.save(this.form.getRawValue());
    if (result && this.isNew) await this.router.navigate(["/management/branches", result.branch.id]);
  }
  protected refreshVersion(): Promise<void> { return this.facade.refreshVersionPreservingDraft(); }
  protected async changeStatus(action: "activate" | "deactivate"): Promise<void> {
    const changed = action === "activate" ? await this.facade.activate() : await this.facade.deactivate();
    if (changed) this.confirmation.set(null);
  }
}
