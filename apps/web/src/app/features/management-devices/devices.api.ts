import {
  HttpClient,
  HttpHeaders,
  HttpParams,
  HttpResponse,
} from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { map, Observable } from "rxjs";
import {
  DeviceDetail,
  DeviceListResponse,
  DeviceOptions,
  DeviceWriteRequest,
  PairingChallenge,
  VersionedDevice,
} from "./devices.models";

@Injectable({ providedIn: "root" })
export class DevicesApi {
  private readonly http = inject(HttpClient);
  private readonly base = "/api/v1/management/devices";
  list(
    status: string,
    branchId: string,
    search: string,
    page: number,
  ): Observable<DeviceListResponse> {
    let params = new HttpParams()
      .set("status", status)
      .set("page", page)
      .set("pageSize", 25);
    if (branchId) params = params.set("branchId", branchId);
    if (search.trim()) params = params.set("search", search.trim());
    return this.http.get<DeviceListResponse>(this.base, { params });
  }
  options(): Observable<DeviceOptions> {
    return this.http.get<DeviceOptions>(`${this.base}/options`);
  }
  get(id: string): Observable<VersionedDevice> {
    return this.http
      .get<DeviceDetail>(`${this.base}/${id}`, { observe: "response" })
      .pipe(map((r) => this.versioned(r)));
  }
  create(value: DeviceWriteRequest): Observable<VersionedDevice> {
    return this.http
      .post<DeviceDetail>(this.base, value, { observe: "response" })
      .pipe(map((r) => this.versioned(r)));
  }
  update(
    id: string,
    etag: string,
    value: DeviceWriteRequest,
  ): Observable<VersionedDevice> {
    return this.http
      .put<DeviceDetail>(`${this.base}/${id}`, value, {
        observe: "response",
        headers: this.match(etag),
      })
      .pipe(map((r) => this.versioned(r)));
  }
  challenge(id: string): Observable<PairingChallenge> {
    return this.http.post<PairingChallenge>(
      `${this.base}/${id}/pairing-challenge`,
      null,
    );
  }
  revoke(id: string, etag: string): Observable<string> {
    return this.http
      .post<void>(`${this.base}/${id}/revoke`, null, {
        observe: "response",
        headers: this.match(etag),
      })
      .pipe(map((r) => r.headers.get("ETag") ?? ""));
  }
  private match(etag: string): HttpHeaders {
    return new HttpHeaders({ "If-Match": etag });
  }
  private versioned(response: HttpResponse<DeviceDetail>): VersionedDevice {
    const etag = response.headers.get("ETag");
    if (!response.body || !etag)
      throw new Error("Device response is incomplete.");
    return { device: response.body, etag };
  }
}
