import { Injectable, signal } from "@angular/core";

@Injectable({ providedIn: "root" })
export class CsrfTokenStore {
  private readonly tokenState = signal<string | null>(null);
  readonly token = this.tokenState.asReadonly();

  replace(token: string): void {
    this.tokenState.set(token);
  }

  clear(): void {
    this.tokenState.set(null);
  }
}
