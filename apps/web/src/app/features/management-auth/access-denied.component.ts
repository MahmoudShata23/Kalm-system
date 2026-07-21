import { ChangeDetectionStrategy, Component, computed, inject, signal } from "@angular/core";
import { Router } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { LanguageService } from "../../core/i18n/language.service";

@Component({
  selector: "kalm-access-denied",
  standalone: true,
  imports: [ButtonModule],
  templateUrl: "./access-denied.component.html",
  styleUrl: "./access-denied.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccessDeniedComponent {
  private readonly auth = inject(ManagementAuthService);
  private readonly language = inject(LanguageService);
  private readonly router = inject(Router);

  protected readonly busy = signal(false);
  protected readonly copy = computed(() => this.language.copy().accessDenied);

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
