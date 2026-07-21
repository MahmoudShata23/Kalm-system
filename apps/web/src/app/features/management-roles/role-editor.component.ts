import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormControl, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { LanguageService } from "../../core/i18n/language.service";
import { ROLE_GROUP_LABELS, ROLES_COPY } from "./management-roles.copy";
import { RolesFacade } from "./roles.facade";
import { PermissionPresentation, RoleWriteRequest } from "./roles.models";

interface PermissionGroup {
  code: string;
  order: number;
  label: string;
  permissions: PermissionPresentation[];
}

@Component({
  selector: "kalm-role-editor",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonModule, InputTextModule],
  templateUrl: "./role-editor.component.html",
  styleUrl: "./role-editor.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RoleEditorComponent implements OnInit {
  private readonly facade = inject(RolesFacade);
  private readonly language = inject(LanguageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roleId = this.route.snapshot.paramMap.get("roleId") ?? undefined;

  protected readonly name = new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] });
  protected readonly selectedCodes = signal<ReadonlySet<string>>(new Set());
  protected readonly permissionSearch = signal("");
  protected readonly archiveConfirmation = signal(false);
  protected readonly submitted = signal(false);
  protected readonly copy = computed(() => ROLES_COPY[this.language.language()]);
  protected readonly detail = this.facade.detail;
  protected readonly loading = this.facade.loading;
  protected readonly saving = this.facade.saving;
  protected readonly errorCode = this.facade.errorCode;
  protected readonly conflictEtag = this.facade.conflictEtag;
  protected readonly announcement = computed(() => {
    const value = this.facade.announcement();
    return value === "saved" ? this.copy().saved : value === "archived" ? this.copy().archivedNotice : "";
  });
  protected readonly isNew = !this.roleId;
  protected readonly readOnly = computed(() => {
    const role = this.detail()?.role;
    return !!role && (role.isProtectedSystemRole || role.status === "archived");
  });
  protected readonly groups = computed<PermissionGroup[]>(() => {
    const language = this.language.language();
    const query = this.permissionSearch().trim().toLocaleLowerCase(language);
    const entries = (this.facade.catalogue()?.permissions ?? []).filter(permission => {
      const label = language === "ar" ? permission.arabicLabel : permission.englishLabel;
      const description = language === "ar" ? permission.arabicDescription : permission.englishDescription;
      return !query || `${label} ${description} ${permission.code}`.toLocaleLowerCase(language).includes(query);
    });
    const groups = new Map<string, PermissionGroup>();
    for (const permission of entries) {
      const existing = groups.get(permission.groupCode) ?? {
        code: permission.groupCode,
        order: permission.groupOrder,
        label: ROLE_GROUP_LABELS[language][permission.groupCode] ?? permission.groupCode,
        permissions: []
      };
      existing.permissions.push(permission);
      groups.set(permission.groupCode, existing);
    }
    return [...groups.values()].sort((left, right) => left.order - right.order);
  });

  async ngOnInit(): Promise<void> {
    await this.facade.loadEditor(this.roleId);
    this.populateFromDetail();
  }

  protected permissionLabel(permission: PermissionPresentation): string {
    return this.language.language() === "ar" ? permission.arabicLabel : permission.englishLabel;
  }

  protected permissionDescription(permission: PermissionPresentation): string {
    return this.language.language() === "ar" ? permission.arabicDescription : permission.englishDescription;
  }

  protected togglePermission(code: string, selected: boolean): void {
    if (this.readOnly()) return;
    this.selectedCodes.update(current => {
      const next = new Set(current);
      if (selected) next.add(code);
      else next.delete(code);
      return next;
    });
  }

  protected async save(): Promise<void> {
    this.submitted.set(true);
    if (this.name.invalid || this.selectedCodes().size === 0 || this.readOnly()) return;
    const request: RoleWriteRequest = {
      name: this.name.value,
      permissionCodes: [...this.selectedCodes()].sort()
    };
    const saved = this.isNew ? await this.facade.create(request) : await this.facade.update(request);
    if (saved && this.isNew) {
      await this.router.navigate(["/management/roles", saved.id]);
    }
  }

  protected async reloadLatest(): Promise<void> {
    if (!this.roleId) return;
    await this.facade.loadEditor(this.roleId);
    this.populateFromDetail();
  }

  protected async archive(): Promise<void> {
    if (!await this.facade.archive()) return;
    await this.router.navigate(["/management/roles"]);
  }

  protected errorMessage(): string {
    return this.errorCode() === "role.has_active_assignments" ? this.copy().assignedArchive : this.copy().saveError;
  }

  private populateFromDetail(): void {
    const role = this.detail()?.role;
    if (!role) {
      if (this.isNew) {
        this.name.setValue("");
        this.selectedCodes.set(new Set());
      }
      return;
    }
    this.name.setValue(role.name);
    this.selectedCodes.set(new Set(role.permissionCodes));
  }
}
