import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from "@angular/core";
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import { firstValueFrom } from "rxjs";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { LanguageService } from "../../core/i18n/language.service";
import {
  DeviceAuthApi,
  EligibleEmployees,
  PinLoginResponse,
} from "./device-auth.api";
@Component({
  selector: "kalm-workstation",
  standalone: true,
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<main class="station">
    <button type="button" class="language" (click)="toggleLanguage()">
      {{ language.language() === "ar" ? "EN" : "AR" }}
    </button>
    <h1>{{ c().heading }}</h1>
    @if (mode() === "pair") {
      <form [formGroup]="pairForm" (ngSubmit)="pair()">
        <label
          >{{ c().deviceId
          }}<input
            pInputText
            formControlName="deviceId"
            autocomplete="off" /></label
        ><label
          >{{ c().pairing
          }}<input
            pInputText
            formControlName="challenge"
            autocomplete="one-time-code" /></label
        ><p-button type="submit" [label]="c().pair" [loading]="busy()" />
      </form>
    } @else if (mode() === "login" || mode() === "locked") {
      @if (!selected()) {
        <div class="employees">
          @for (user of eligible()?.items ?? []; track user.id) {
            <button type="button" (click)="selected.set(user.id)">
              {{ user.displayName }}
            </button>
          } @empty {
            <p>{{ c().empty }}</p>
          }
        </div>
      } @else {
        <form [formGroup]="pinForm" (ngSubmit)="login()">
          <label
            >{{ c().pin
            }}<input
              pInputText
              type="password"
              inputmode="numeric"
              maxlength="6"
              autocomplete="one-time-code"
              formControlName="pin" /></label
          ><p-button
            type="submit"
            [label]="c().login"
            [loading]="busy()"
          /><p-button
            type="button"
            [label]="c().back"
            (onClick)="selected.set(null)"
          />
        </form>
      }
    } @else {
      <p>{{ c().ready }} {{ session()?.displayName ?? auth.user().displayName }}</p>
      <p-button
        type="button"
        icon="pi pi-lock"
        [label]="c().lock"
        (onClick)="lock()"
      />
    }
    @if (error()) {
      <p role="alert">{{ c().error }}</p>
    }
  </main>`,
  styles: [
    `
      :host {
        display: grid;
        min-height: 100dvh;
        place-items: center;
      }
      .station {
        width: min(34rem, 92vw);
        display: grid;
        gap: 1.2rem;
        padding: 1.5rem;
        border-radius: 16px;
        background: var(--p-surface-0);
        box-shadow: var(--p-overlay-popover-shadow);
      }
      form,
      label {
        display: grid;
        gap: 0.6rem;
      }
      input,
      button {
        min-height: 44px;
      }
      .employees {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
        gap: 0.75rem;
      }
      .employees button {
        padding: 1rem;
        border: 1px solid var(--p-primary-300);
        border-radius: 12px;
        background: var(--p-surface-50);
      }
      .language {
        justify-self: end;
      }
    `,
  ],
})
export class WorkstationComponent implements OnInit {
  private readonly api = inject(DeviceAuthApi);
  protected readonly auth = inject(ManagementAuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);
  protected readonly mode = signal(
    String(this.route.snapshot.data["mode"] ?? "login"),
  );
  protected readonly eligible = signal<EligibleEmployees | null>(null);
  protected readonly selected = signal<string | null>(null);
  protected readonly session = signal<PinLoginResponse | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal(false);
  protected readonly pairForm = new FormGroup({
    deviceId: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required],
    }),
    challenge: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(128)],
    }),
  });
  protected readonly pinForm = new FormGroup({
    pin: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\d{6}$/)],
    }),
  });
  protected readonly c = computed(() =>
    this.language.language() === "ar"
      ? {
          heading: this.mode() === "pair" ? "اقتران الجهاز" : "محطة كالم",
          deviceId: "معرف الجهاز",
          pairing: "رمز الاقتران",
          pair: "اقتران",
          pin: "رمز الموظف",
          login: "دخول",
          back: "اختيار موظف آخر",
          empty: "لا يوجد موظفون مؤهلون.",
          ready: "المحطة جاهزة،",
          lock: "قفل وتبديل الموظف",
          error: "تعذر التحقق. حاول مرة أخرى.",
        }
      : {
          heading: this.mode() === "pair" ? "Pair device" : "Kalm workstation",
          deviceId: "Device ID",
          pairing: "Pairing value",
          pair: "Pair",
          pin: "Employee PIN",
          login: "Sign in",
          back: "Choose another employee",
          empty: "No eligible employees.",
          ready: "Workstation ready,",
          lock: "Lock and switch employee",
          error: "Verification failed. Try again.",
        },
  );
  async ngOnInit(): Promise<void> {
    await this.auth.ensureInitialized();
    if (this.mode() === "login" || this.mode() === "locked")
      await this.loadEligible();
  }
  private async loadEligible(): Promise<void> {
    try {
      this.eligible.set(await firstValueFrom(this.api.eligible()));
    } catch {
      this.error.set(true);
    }
  }
  protected async pair(): Promise<void> {
    if (this.pairForm.invalid) return;
    this.busy.set(true);
    this.error.set(false);
    const v = this.pairForm.getRawValue();
    try {
      await firstValueFrom(this.api.pair(v.deviceId, v.challenge));
      this.pairForm.reset({ deviceId: "", challenge: "" });
      await this.router.navigate(["/workstation/login"]);
    } catch {
      this.error.set(true);
    } finally {
      this.busy.set(false);
    }
  }
  protected async login(): Promise<void> {
    if (this.pinForm.invalid || !this.selected()) return;
    this.busy.set(true);
    this.error.set(false);
    try {
      this.session.set(
        await firstValueFrom(
          this.api.pinLogin(this.selected()!, this.pinForm.value.pin!),
        ),
      );
      await this.auth.refreshCurrentUser();
      this.pinForm.reset({ pin: "" });
      await this.router.navigate(["/workstation"]);
      this.mode.set("ready");
    } catch {
      this.pinForm.reset({ pin: "" });
      this.error.set(true);
    } finally {
      this.busy.set(false);
    }
  }
  protected async lock(): Promise<void> {
    await firstValueFrom(this.api.lock());
    this.session.set(null);
    this.selected.set(null);
    await this.router.navigate(["/workstation/locked"]);
    this.mode.set("locked");
    await this.loadEligible();
  }
  protected toggleLanguage(): void {
    this.language.setLanguage(this.language.language() === "ar" ? "en" : "ar");
  }
}
