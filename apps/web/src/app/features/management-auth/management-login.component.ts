import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal, viewChild } from "@angular/core";
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";
import { PasswordModule } from "primeng/password";
import { Router } from "@angular/router";
import { ManagementAuthService } from "../../core/auth/management-auth.service";
import { MANAGEMENT_ACCESS_PERMISSION } from "../../core/auth/management-permissions";
import { LanguageService } from "../../core/i18n/language.service";

@Component({
  selector: "kalm-management-login",
  standalone: true,
  imports: [ButtonModule, InputTextModule, PasswordModule, ReactiveFormsModule],
  templateUrl: "./management-login.component.html",
  styleUrl: "./management-login.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManagementLoginComponent {
  private readonly auth = inject(ManagementAuthService);
  private readonly language = inject(LanguageService);
  private readonly router = inject(Router);
  private readonly identifierInput = viewChild<ElementRef<HTMLInputElement>>("identifierInput");
  private readonly passwordHost = viewChild<ElementRef<HTMLElement>>("passwordHost");

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal("");
  protected readonly user = this.auth.user;
  protected readonly copy = computed(() => this.language.copy().managementLogin);
  protected readonly form = new FormGroup({
    identifier: new FormControl("", { nonNullable: true, validators: [Validators.required, Validators.maxLength(254)] }),
    password: new FormControl("", { nonNullable: true, validators: [Validators.required] })
  });

  protected async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set("");
    try {
      await this.auth.login(this.form.getRawValue());
      await this.router.navigate([
        this.auth.hasPermission(MANAGEMENT_ACCESS_PERMISSION) ? "/management" : "/management/access-denied"
      ]);
    } catch {
      this.errorMessage.set(this.copy().genericError);
      queueMicrotask(() => this.identifierInput()?.nativeElement.focus());
    } finally {
      this.clearPassword();
      this.submitting.set(false);
    }
  }

  protected async logout(): Promise<void> {
    if (this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set("");
    try {
      await this.auth.logout();
      queueMicrotask(() => this.identifierInput()?.nativeElement.focus());
    } catch {
      this.errorMessage.set(this.copy().logoutError);
    } finally {
      this.clearPassword();
      this.submitting.set(false);
    }
  }

  private clearPassword(): void {
    this.form.controls.password.setValue("");
    const input = this.passwordHost()?.nativeElement.querySelector<HTMLInputElement>("input");
    if (input) {
      input.value = "";
    }
  }
}
