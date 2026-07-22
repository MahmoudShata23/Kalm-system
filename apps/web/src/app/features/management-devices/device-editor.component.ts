import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from "@angular/core";
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { LanguageService } from "../../core/i18n/language.service";
import { DevicesFacade } from "./devices.facade";

@Component({
  selector: "kalm-device-editor",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonModule, InputTextModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="editor">
    <a routerLink="/management/devices">← {{ c().back }}</a>
    <h2>{{ isNew ? c().create : c().edit }}</h2>
    @if (loading()) {
      <p>{{ c().loading }}</p>
    } @else {
      <form [formGroup]="form" (ngSubmit)="save()">
        <label>{{ c().name }}<input pInputText formControlName="name" /></label
        ><label
          >{{ c().branch
          }}<select formControlName="branchId">
            @for (b of options()?.branches ?? []; track b.id) {
              <option [value]="b.id">{{ b.name }} — {{ b.code }}</option>
            }
          </select></label
        ><label
          >{{ c().type
          }}<select formControlName="type">
            @for (t of options()?.types ?? []; track t.code) {
              <option [value]="t.code">
                {{ language.language() === "ar" ? t.nameAr : t.nameEn }}
              </option>
            }
          </select></label
        ><label
          >{{ c().platform
          }}<input pInputText formControlName="platform" /></label
        ><p-button
          type="submit"
          [label]="c().save"
          [loading]="saving()"
          [disabled]="form.invalid || detail()?.device?.status === 'revoked'"
        />
      </form>
      @if (detail(); as d) {
        <div class="actions">
          <p>{{ c().status }}: {{ d.device.status }}</p>
          @if (d.device.status !== "revoked") {
            <p-button
              type="button"
              icon="pi pi-link"
              [label]="c().challenge"
              (onClick)="challenge()"
            /><p-button
              type="button"
              severity="danger"
              icon="pi pi-ban"
              [label]="c().revoke"
              (onClick)="confirmRevoke.set(true)"
            />
          }
        </div>
      }
      @if (pairing(); as p) {
        <aside role="status">
          <h3>{{ c().oneTime }}</h3>
          <code>{{ p.pairingChallenge }}</code>
          <p>{{ c().expires }} {{ remaining() }}</p>
          <p>{{ c().warning }}</p>
          <p-button
            type="button"
            [label]="c().dismiss"
            (onClick)="facade.clearChallenge()"
          />
        </aside>
      }
      @if (confirmRevoke()) {
        <div role="dialog">
          <p>{{ c().confirm }}</p>
          <p-button
            type="button"
            severity="danger"
            [label]="c().revoke"
            (onClick)="revoke()"
          /><p-button
            type="button"
            [label]="c().dismiss"
            (onClick)="confirmRevoke.set(false)"
          />
        </div>
      }
      @if (error()) {
        <p role="alert">{{ c().error }}</p>
      }
    }
  </section>`,
  styles: [
    `
      :host {
        display: block;
      }
      .editor,
      form {
        display: grid;
        gap: 1rem;
        max-width: 48rem;
      }
      label {
        display: grid;
        gap: 0.35rem;
      }
      input,
      select,
      button {
        min-height: 44px;
      }
      .actions {
        display: flex;
        gap: 0.75rem;
        flex-wrap: wrap;
      }
      aside,
      [role="dialog"] {
        padding: 1rem;
        border: 1px solid var(--p-primary-300);
        border-radius: 12px;
        background: var(--p-surface-50);
      }
      code {
        font-size: 1.2rem;
        word-break: break-all;
        user-select: all;
      }
    `,
  ],
})
export class DeviceEditorComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(DevicesFacade);
  protected readonly language = inject(LanguageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly id =
    this.route.snapshot.paramMap.get("deviceId") ?? undefined;
  protected readonly isNew = !this.id;
  protected readonly form = new FormGroup({
    name: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    branchId: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required],
    }),
    type: new FormControl("posTerminal", {
      nonNullable: true,
      validators: [Validators.required],
    }),
    platform: new FormControl("", {
      nonNullable: true,
      validators: [Validators.maxLength(120)],
    }),
  });
  protected readonly detail = this.facade.detail;
  protected readonly options = this.facade.options;
  protected readonly pairing = this.facade.pairingChallenge;
  protected readonly loading = this.facade.loading;
  protected readonly saving = this.facade.saving;
  protected readonly error = this.facade.errorCode;
  protected readonly confirmRevoke = signal(false);
  protected readonly now = signal(Date.now());
  private timer?: ReturnType<typeof setInterval>;
  protected readonly remaining = computed(() => {
    const p = this.pairing();
    if (!p) return "";
    const seconds = Math.max(
      0,
      Math.floor((Date.parse(p.expiresAtUtc) - this.now()) / 1000),
    );
    return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
  });
  protected readonly c = computed(() =>
    this.language.language() === "ar"
      ? {
          back: "الأجهزة",
          create: "تسجيل جهاز",
          edit: "تعديل الجهاز",
          loading: "جارٍ التحميل…",
          name: "الاسم",
          branch: "الفرع",
          type: "النوع",
          platform: "المنصة",
          save: "حفظ",
          status: "الحالة",
          challenge: "إنشاء رمز اقتران",
          revoke: "إلغاء الجهاز",
          oneTime: "رمز اقتران لمرة واحدة",
          expires: "ينتهي خلال",
          warning: "انسخ الرمز الآن. لن يظهر مرة أخرى.",
          dismiss: "إغلاق",
          confirm: "سيؤدي الإلغاء إلى إنهاء كل جلسات الجهاز.",
          error: "تعذر إكمال العملية.",
        }
      : {
          back: "Devices",
          create: "Register device",
          edit: "Edit device",
          loading: "Loading…",
          name: "Name",
          branch: "Branch",
          type: "Type",
          platform: "Platform",
          save: "Save",
          status: "Status",
          challenge: "Create pairing challenge",
          revoke: "Revoke device",
          oneTime: "One-time pairing value",
          expires: "Expires in",
          warning: "Copy this value now. It will not be shown again.",
          dismiss: "Dismiss",
          confirm:
            "Revocation immediately ends every session bound to this device.",
          error: "The operation could not be completed.",
        },
  );
  async ngOnInit(): Promise<void> {
    await this.facade.loadEditor(this.id);
    const d = this.detail()?.device;
    if (d)
      this.form.patchValue({
        name: d.name,
        branchId: d.branchId,
        type: d.type,
        platform: d.platform ?? "",
      });
    else {
      const b = this.options()?.branches[0];
      if (b) this.form.controls.branchId.setValue(b.id);
    }
    this.timer = setInterval(() => this.now.set(Date.now()), 1000);
  }
  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
    this.facade.clearChallenge();
  }
  protected async save(): Promise<void> {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const result = await this.facade.save({
      ...v,
      platform: v.platform.trim() || null,
    });
    if (result && this.isNew)
      await this.router.navigate(["/management/devices", result.device.id]);
  }
  protected challenge(): Promise<void> {
    return this.facade.createChallenge();
  }
  protected async revoke(): Promise<void> {
    if (await this.facade.revoke()) this.confirmRevoke.set(false);
  }
}
