import { ChangeDetectionStrategy, Component, computed, inject, signal } from "@angular/core";
import { Router } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { LanguageService } from "../../core/i18n/language.service";

@Component({
  selector: "kalm-management-shell",
  standalone: true,
  imports: [ButtonModule],
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
