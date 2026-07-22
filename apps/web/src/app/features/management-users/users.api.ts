import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { map, Observable } from "rxjs";
import {
  UserCreateRequest,
  UserDetail,
  UserEditorOptions,
  UserListResponse,
  UserWriteRequest,
  VersionedUser
} from "./users.models";

@Injectable({ providedIn: "root" })
export class UsersApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = "/api/v1/management/users";

  list(status: string, search: string, page: number, pageSize: number): Observable<UserListResponse> {
    let params = new HttpParams().set("status", status).set("page", page).set("pageSize", pageSize);
    if (search.trim()) params = params.set("search", search.trim());
    return this.http.get<UserListResponse>(this.baseUrl, { params });
  }

  options(): Observable<UserEditorOptions> {
    return this.http.get<UserEditorOptions>(`${this.baseUrl}/options`);
  }

  get(userId: string): Observable<VersionedUser> {
    return this.http.get<UserDetail>(`${this.baseUrl}/${userId}`, { observe: "response" })
      .pipe(map(response => this.toVersionedUser(response)));
  }

  create(request: UserCreateRequest): Observable<VersionedUser> {
    return this.http.post<UserDetail>(this.baseUrl, request, { observe: "response" })
      .pipe(map(response => this.toVersionedUser(response)));
  }

  update(userId: string, etag: string, request: UserWriteRequest): Observable<VersionedUser> {
    return this.http.put<UserDetail>(`${this.baseUrl}/${userId}`, request, {
      observe: "response",
      headers: this.ifMatch(etag)
    }).pipe(map(response => this.toVersionedUser(response)));
  }

  activate(userId: string, etag: string): Observable<VersionedUser> {
    return this.http.post<UserDetail>(`${this.baseUrl}/${userId}/activate`, null, {
      observe: "response",
      headers: this.ifMatch(etag)
    }).pipe(map(response => this.toVersionedUser(response)));
  }

  suspend(userId: string, etag: string): Observable<VersionedUser> {
    return this.http.post<UserDetail>(`${this.baseUrl}/${userId}/suspend`, { confirmSelfSuspension: true }, {
      observe: "response",
      headers: this.ifMatch(etag)
    }).pipe(map(response => this.toVersionedUser(response)));
  }

  setPassword(userId: string, etag: string, password: string): Observable<string> {
    return this.http.post<void>(`${this.baseUrl}/${userId}/password`, { password }, {
      observe: "response",
      headers: this.ifMatch(etag)
    }).pipe(map(response => {
      const nextEtag = response.headers.get("ETag");
      if (!nextEtag) throw new Error("Password response did not include its authoritative ETag.");
      return nextEtag;
    }));
  }

  private ifMatch(etag: string): HttpHeaders {
    return new HttpHeaders({ "If-Match": etag });
  }

  private toVersionedUser(response: HttpResponse<UserDetail>): VersionedUser {
    const user = response.body;
    const etag = response.headers.get("ETag");
    if (!user || !etag) throw new Error("User response did not include its authoritative ETag.");
    return { user, etag };
  }
}
