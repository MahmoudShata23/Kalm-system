import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { USERS_MANAGE_PERMISSION } from "../../core/auth/management-permissions";
import { LanguageService } from "../../core/i18n/language.service";
import { USERS_COPY } from "./management-users.copy";
import { UsersFacade } from "./users.facade";
import { BranchAccessScope, UserCreateRequest, UserWriteRequest } from "./users.models";

@Component({
  selector: "kalm-user-editor",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonModule, InputTextModule],
  templateUrl: "./user-editor.component.html",
  styleUrl: "./user-editor.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserEditorComponent implements OnInit {
  private readonly facade = inject(UsersFacade);
  private readonly language = inject(LanguageService);
  private readonly auth = inject(ManagementAuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly userId = this.route.snapshot.paramMap.get("userId") ?? undefined;

  protected readonly form = new FormGroup({
    username: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(64)] }),
    email: new FormControl("", { nonNullable: true, validators: [Validators.email, Validators.maxLength(254)] }),
    displayName: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(120)] }),
    preferredLanguage: new FormControl<"en" | "ar">("en", { nonNullable: true, validators: [Validators.required] }),
    branchAccessScope: new FormControl<BranchAccessScope>("assignedBranches", { nonNullable: true, validators: [Validators.required] }),
    initialPassword: new FormControl("", { nonNullable: true, validators: [Validators.minLength(15), Validators.maxLength(128)] })
  });
  protected readonly password = new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.minLength(15), Validators.maxLength(128)] });
  protected readonly pin = new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.pattern(/^\d{6}$/)] });
  protected readonly selectedRoleIds = signal<ReadonlySet<string>>(new Set());
  protected readonly selectedBranchIds = signal<ReadonlySet<string>>(new Set());
  protected readonly submitted = signal(false);
  protected readonly suspendConfirmation = signal(false);
  protected readonly copy = computed(() => USERS_COPY[this.language.language()]);
  protected readonly detail = this.facade.detail;
  protected readonly options = this.facade.options;
  protected readonly loading = this.facade.loading;
  protected readonly saving = this.facade.saving;
  protected readonly errorCode = this.facade.errorCode;
  protected readonly conflictEtag = this.facade.conflictEtag;
  protected readonly isNew = !this.userId;
  protected readonly canManage = computed(() => this.auth.hasPermission(USERS_MANAGE_PERMISSION));
  protected readonly readOnly = computed(() => !this.canManage() || this.detail()?.user.status === "archived");
  protected readonly announcement = computed(() => {
    const value = this.facade.announcement();
    if (value === "saved") return this.copy().saved;
    if (value === "activated") return this.copy().activatedNotice;
    if (value === "suspended") return this.copy().suspendedNotice;
    if (value === "passwordSet") return this.copy().passwordNotice;
    if (value === "pinSet") return this.copy().pinNotice;
    return "";
  });

  async ngOnInit(): Promise<void> {
    await this.facade.loadEditor(this.userId);
    this.populateFromDetail();
  }

  protected toggleRole(id: string, selected: boolean): void {
    if (this.readOnly()) return;
    this.selectedRoleIds.update(current => this.toggle(current, id, selected));
  }

  protected toggleBranch(id: string, selected: boolean): void {
    if (this.readOnly()) return;
    this.selectedBranchIds.update(current => this.toggle(current, id, selected));
  }

  protected changeScope(scope: BranchAccessScope): void {
    this.form.controls.branchAccessScope.setValue(scope);
    if (scope === "allOrganizationBranches") this.selectedBranchIds.set(new Set());
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    if (this.form.invalid || this.selectedRoleIds().size === 0 || this.branchSelectionInvalid() || this.readOnly()) return;
    const value = this.form.getRawValue();
    const base: UserWriteRequest = {
      username: value.username,
      email: value.email.trim() || null,
      displayName: value.displayName,
      preferredLanguage: value.preferredLanguage,
      roleIds: [...this.selectedRoleIds()].sort(),
      branchAccessScope: value.branchAccessScope,
      branchIds: value.branchAccessScope === "assignedBranches" ? [...this.selectedBranchIds()].sort() : []
    };
    const saved = this.isNew
      ? await this.facade.create({ ...base, initialPassword: value.initialPassword || null } satisfies UserCreateRequest)
      : await this.facade.update(base);
    this.form.controls.initialPassword.setValue("");
    if (saved && this.isNew) await this.router.navigate(["/management/users", saved.id]);
  }

  protected async setPassword(): Promise<void> {
    this.password.markAsTouched();
    if (this.password.invalid || this.readOnly()) return;
    await this.facade.setPassword(this.password.value);
    this.password.setValue("");
    this.password.markAsUntouched();
  }

  protected async setPin(): Promise<void> {
    this.pin.markAsTouched(); if (this.pin.invalid || this.readOnly()) return;
    await this.facade.setPin(this.pin.value); this.pin.setValue(""); this.pin.markAsUntouched();
  }

  protected async activate(): Promise<void> { await this.facade.activate(); }

  protected async suspend(): Promise<void> {
    const result = await this.facade.suspend();
    if (result) this.suspendConfirmation.set(false);
  }

  protected async reloadLatest(): Promise<void> {
    if (!this.userId) return;
    await this.facade.loadEditor(this.userId);
    this.populateFromDetail();
  }

  protected branchSelectionInvalid(): boolean {
    return this.form.controls.branchAccessScope.value === "assignedBranches" && this.selectedBranchIds().size === 0;
  }

  protected errorMessage(): string {
    if (this.errorCode() === "user.activation_requirements_not_met") return this.copy().activationRequirements;
    if (this.errorCode() === "user.last_management_access") return this.copy().lastManagement;
    if (this.errorCode() === "user.reauthentication_required") return this.copy().reauthentication;
    return this.copy().saveError;
  }

  private populateFromDetail(): void {
    const user = this.detail()?.user;
    if (!user) {
      this.form.reset({ username: "", email: "", displayName: "", preferredLanguage: "en", branchAccessScope: "assignedBranches", initialPassword: "" });
      this.selectedRoleIds.set(new Set());
      this.selectedBranchIds.set(new Set());
      return;
    }
    this.form.patchValue({
      username: user.username,
      email: user.email ?? "",
      displayName: user.displayName,
      preferredLanguage: user.preferredLanguage,
      branchAccessScope: user.branchAccessScope,
      initialPassword: ""
    });
    this.selectedRoleIds.set(new Set(user.roleIds));
    this.selectedBranchIds.set(new Set(user.branchIds));
  }

  private toggle(current: ReadonlySet<string>, id: string, selected: boolean): ReadonlySet<string> {
    const next = new Set(current);
    if (selected) next.add(id); else next.delete(id);
    return next;
  }
}
