import { HttpClient } from "@angular/common/http";
import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { CsrfTokenStore } from "./csrf-token.store";
import { CsrfTokenResponse, CurrentUser, LoginRequest } from "./auth.models";
import { LanguageService } from "../i18n/language.service";

const ANONYMOUS: CurrentUser = {
  isAuthenticated: false,
  username: null,
  displayName: null,
  preferredLanguage: null,
  inactivityExpiresAtUtc: null,
  absoluteExpiresAtUtc: null,
  reauthenticationValidUntilUtc: null,
  permissions: [],
  branchAccess: null
};

@Injectable({ providedIn: "root" })
export class ManagementAuthService {
  private readonly http = inject(HttpClient);
  private readonly csrf = inject(CsrfTokenStore);
  private readonly language = inject(LanguageService);
  private readonly userState = signal<CurrentUser>(ANONYMOUS);
  private readonly initializedState = signal(false);
  private refreshPromise: Promise<void> | null = null;
  private initializePromise: Promise<void> | null = null;

  readonly user = this.userState.asReadonly();
  readonly initialized = this.initializedState.asReadonly();

  async initialize(): Promise<void> {
    await this.ensureInitialized();
  }

  ensureInitialized(): Promise<void> {
    this.initializePromise ??= Promise.all([this.refreshCsrf().catch(() => undefined), this.refreshCurrentUser()])
      .then(() => undefined)
      .finally(() => this.initializedState.set(true));
    return this.initializePromise;
  }

  async login(request: LoginRequest): Promise<void> {
    if (!this.csrf.token()) {
      await this.refreshCsrf();
    }

    const user = await firstValueFrom(this.http.post<CurrentUser>("/api/v1/auth/login", request));
    this.setUser(user);
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
      this.setUser(await firstValueFrom(this.http.get<CurrentUser>("/api/v1/auth/me")));
    } catch {
      this.userState.set(ANONYMOUS);
    }
  }

  hasPermission(permission: string): boolean {
    return this.userState().permissions.includes(permission);
  }

  private setUser(user: CurrentUser): void {
    this.userState.set({ ...user, permissions: [...user.permissions].sort() });
    if (user.preferredLanguage) {
      this.language.setLanguage(user.preferredLanguage);
    }
  }

  refreshCsrf(): Promise<void> {
    this.refreshPromise ??= firstValueFrom(this.http.get<CsrfTokenResponse>("/api/v1/auth/csrf"))
      .then(response => this.csrf.replace(response.requestToken))
      .finally(() => this.refreshPromise = null);
    return this.refreshPromise;
  }
}
