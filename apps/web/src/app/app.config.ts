import { ApplicationConfig, provideZonelessChangeDetection } from "@angular/core";
import { provideAnimationsAsync } from "@angular/platform-browser/animations/async";
import { provideRouter } from "@angular/router";
import { providePrimeNG } from "primeng/config";
import { routes } from "./app.routes";
import { KalmPreset } from "./core/theme/kalm-preset";

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: KalmPreset,
        options: { darkModeSelector: false, cssLayer: false }
      },
      ripple: true
    }),
    provideRouter(routes)
  ]
};
