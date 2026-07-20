import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from "@angular/core";
import { ButtonModule } from "primeng/button";
import { RouterOutlet } from "@angular/router";
import { Language, LanguageService } from "./core/i18n/language.service";
import { ManagementAuthService } from "./core/auth/management-auth.service";

@Component({
  selector: "kalm-root",
  standalone: true,
  imports: [ButtonModule, RouterOutlet],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent implements OnInit {
  private readonly languageService = inject(LanguageService);
  private readonly auth = inject(ManagementAuthService);

  protected readonly viewModel = computed(() => ({
    language: this.languageService.language(),
    direction: this.languageService.direction(),
    copy: this.languageService.copy(),
    isAuthenticated: this.auth.user().isAuthenticated
  }));

  ngOnInit(): void {
    void this.auth.initialize().catch(() => undefined);
  }

  protected setLanguage(language: Language): void {
    this.languageService.setLanguage(language);
  }
}
