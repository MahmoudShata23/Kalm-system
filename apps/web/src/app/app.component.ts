import { ChangeDetectionStrategy, Component, computed, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatToolbarModule } from "@angular/material/toolbar";
import { Language, LanguageService } from "./core/i18n/language.service";

@Component({
  selector: "kalm-root",
  standalone: true,
  imports: [MatButtonModule, MatToolbarModule],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {
  private readonly languageService = inject(LanguageService);

  protected readonly viewModel = computed(() => ({
    language: this.languageService.language(),
    direction: this.languageService.direction(),
    copy: this.languageService.copy()
  }));

  protected setLanguage(language: Language): void {
    this.languageService.setLanguage(language);
  }
}
