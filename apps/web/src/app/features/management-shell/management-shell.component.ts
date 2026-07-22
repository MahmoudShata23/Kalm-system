import { ChangeDetectionStrategy, Component, computed, inject, signal } from "@angular/core";
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { LanguageService } from "../../core/i18n/language.service";
import { DEVICES_MANAGE_PERMISSION, ROLES_MANAGE_PERMISSION, USERS_VIEW_PERMISSION } from "../../core/auth/management-permissions";
import { ROLES_COPY } from "../management-roles/management-roles.copy";
import { USERS_COPY } from "../management-users/management-users.copy";

@Component({
  selector: "kalm-management-shell",
  standalone: true,
  imports: [ButtonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: "./management-shell.component.html",
  styleUrl: "./management-shell.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManagementShellComponent {
  private readonly auth = inject(ManagementAuthService);
  private readonly language = inject(LanguageService);
  private readonly router = inject(Router);

  protected readonly busy = signal(false);
  protected readonly user = this.auth.user;
  protected readonly copy = computed(() => this.language.copy().managementShell);
  protected readonly rolesCopy = computed(() => ROLES_COPY[this.language.language()]);
  protected readonly usersCopy = computed(() => USERS_COPY[this.language.language()]);
  protected readonly canManageRoles = computed(() => this.auth.hasPermission(ROLES_MANAGE_PERMISSION));
  protected readonly canViewUsers = computed(() => this.auth.hasPermission(USERS_VIEW_PERMISSION));
  protected readonly canManageDevices = computed(() => this.auth.hasPermission(DEVICES_MANAGE_PERMISSION));
  protected readonly devicesLabel = computed(() => this.language.language() === "ar" ? "الأجهزة" : "Devices");
  protected readonly scopeLabel = computed(() => this.user().branchAccess?.scope === "allOrganizationBranches"
    ? this.copy().allBranches
    : this.copy().assignedBranches);

  protected async logout(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    try {
      await this.auth.logout();
      await this.router.navigate(["/management/login"]);
    } finally {
      this.busy.set(false);
    }
  }
}
