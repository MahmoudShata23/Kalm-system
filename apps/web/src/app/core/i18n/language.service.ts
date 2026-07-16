import { computed, Injectable, signal } from "@angular/core";
import { TRANSLATIONS } from "./translations";

export type Language = "en" | "ar";
export type Direction = "ltr" | "rtl";

@Injectable({ providedIn: "root" })
export class LanguageService {
  private readonly selectedLanguage = signal<Language>("en");

  readonly language = this.selectedLanguage.asReadonly();

  readonly direction = computed<Direction>(() => this.selectedLanguage() === "ar" ? "rtl" : "ltr");

  readonly copy = computed(() => TRANSLATIONS[this.selectedLanguage()]);

  setLanguage(language: Language): void {
    this.selectedLanguage.set(language);
  }
}
