import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
export interface EligibleEmployee {
  id: string;
  displayName: string;
}
export interface EligibleEmployees {
  items: EligibleEmployee[];
}
export interface PinLoginResponse {
  displayName: string;
  preferredLanguage: "en" | "ar";
}
@Injectable({ providedIn: "root" })
export class DeviceAuthApi {
  private readonly http = inject(HttpClient);
  pair(deviceId: string, pairingChallenge: string): Observable<void> {
    return this.http.post<void>("/api/v1/devices/pair", {
      deviceId,
      pairingChallenge,
    });
  }
  eligible(): Observable<EligibleEmployees> {
    return this.http.get<EligibleEmployees>("/api/v1/devices/eligible-users");
  }
  pinLogin(userId: string, pin: string): Observable<PinLoginResponse> {
    return this.http.post<PinLoginResponse>("/api/v1/auth/pin-login", {
      userId,
      pin,
    });
  }
  lock(): Observable<void> {
    return this.http.post<void>("/api/v1/auth/lock", null);
  }
}
