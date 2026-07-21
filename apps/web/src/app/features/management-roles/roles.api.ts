import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { map, Observable } from "rxjs";
import {
  PermissionCatalogueResponse,
  RoleDetail,
  RoleListResponse,
  RoleWriteRequest,
  VersionedRole
} from "./roles.models";

@Injectable({ providedIn: "root" })
export class RolesApi {
  private readonly http = inject(HttpClient);

  list(status: string, search: string, page: number, pageSize: number): Observable<RoleListResponse> {
    let params = new HttpParams()
      .set("status", status)
      .set("page", page)
      .set("pageSize", pageSize);
    if (search.trim()) params = params.set("search", search.trim());
    return this.http.get<RoleListResponse>("/api/v1/management/roles", { params });
  }

  get(roleId: string): Observable<VersionedRole> {
    return this.http.get<RoleDetail>(`/api/v1/management/roles/${roleId}`, { observe: "response" })
      .pipe(map(response => this.toVersionedRole(response)));
  }

  create(request: RoleWriteRequest): Observable<VersionedRole> {
    return this.http.post<RoleDetail>("/api/v1/management/roles", request, { observe: "response" })
      .pipe(map(response => this.toVersionedRole(response)));
  }

  update(roleId: string, etag: string, request: RoleWriteRequest): Observable<VersionedRole> {
    return this.http.put<RoleDetail>(`/api/v1/management/roles/${roleId}`, request, {
      observe: "response",
      headers: new HttpHeaders({ "If-Match": etag })
    }).pipe(map(response => this.toVersionedRole(response)));
  }

  archive(roleId: string, etag: string): Observable<void> {
    return this.http.post<void>(`/api/v1/management/roles/${roleId}/archive`, null, {
      headers: new HttpHeaders({ "If-Match": etag })
    });
  }

  permissions(): Observable<PermissionCatalogueResponse> {
    return this.http.get<PermissionCatalogueResponse>("/api/v1/management/permissions");
  }

  private toVersionedRole(response: HttpResponse<RoleDetail>): VersionedRole {
    const role = response.body;
    const etag = response.headers.get("ETag");
    if (!role || !etag) throw new Error("Role response did not include its authoritative ETag.");
    return { role, etag };
  }
}
