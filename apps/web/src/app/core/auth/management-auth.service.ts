import { HttpClient } from "@angular/common/http";
import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { CsrfTokenStore } from "./csrf-token.store";
import { CsrfTokenResponse, CurrentUser, LoginRequest } from "./auth.models";

const ANONYMOUS: CurrentUser = {
  isAuthenticated: false,
  username: null,
  displayName: null,
  preferredLanguage: null,
  inactivityExpiresAtUtc: null,
  absoluteExpiresAtUtc: null,
  reauthenticationValidUntilUtc: null,
  permissions: []
};

@Injectable({ providedIn: "root" })
export class ManagementAuthService {
  private readonly http = inject(HttpClient);
  private readonly csrf = inject(CsrfTokenStore);
  private readonly userState = signal<CurrentUser>(ANONYMOUS);
  private refreshPromise: Promise<void> | null = null;

  readonly user = this.userState.asReadonly();

  async initialize(): Promise<void> {
    await Promise.all([this.refreshCsrf(), this.refreshCurrentUser()]);
  }

  async login(request: LoginRequest): Promise<void> {
    if (!this.csrf.token()) {
      await this.refreshCsrf();
    }

    const user = await firstValueFrom(this.http.post<CurrentUser>("/api/v1/auth/login", request));
    this.userState.set(user);
    await this.refreshCsrf();
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.http.post<void>("/api/v1/auth/logout", null));
    this.userState.set(ANONYMOUS);
    this.csrf.clear();
    await this.refreshCsrf();
  }

  async refreshCurrentUser(): Promise<void> {
    try {
      this.userState.set(await firstValueFrom(this.http.get<CurrentUser>("/api/v1/auth/me")));
    } catch {
      this.userState.set(ANONYMOUS);
    }
  }

  refreshCsrf(): Promise<void> {
    this.refreshPromise ??= firstValueFrom(this.http.get<CsrfTokenResponse>("/api/v1/auth/csrf"))
      .then(response => this.csrf.replace(response.requestToken))
      .finally(() => this.refreshPromise = null);
    return this.refreshPromise;
  }
}
