import { ApplicationConfig, provideZonelessChangeDetection } from "@angular/core";
import { provideAnimationsAsync } from "@angular/platform-browser/animations/async";
import { provideHttpClient, withInterceptors } from "@angular/common/http";
import { provideRouter } from "@angular/router";
import { providePrimeNG } from "primeng/config";
import { routes } from "./app.routes";
import { KalmPreset } from "./core/theme/kalm-preset";
import { csrfInterceptor } from "./core/auth/csrf.interceptor";

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([csrfInterceptor])),
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
