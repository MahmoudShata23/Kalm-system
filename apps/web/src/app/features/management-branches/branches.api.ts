import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { map, Observable } from "rxjs";
import { BranchDetail, BranchListResponse, BranchWriteRequest, VersionedBranch } from "./branches.models";

@Injectable({ providedIn: "root" })
export class BranchesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = "/api/v1/management/branches";

  list(status: string, search: string, page: number, pageSize: number): Observable<BranchListResponse> {
    let params = new HttpParams().set("status", status).set("page", page).set("pageSize", pageSize);
    if (search.trim()) params = params.set("search", search.trim());
    return this.http.get<BranchListResponse>(this.baseUrl, { params });
  }

  get(branchId: string): Observable<VersionedBranch> {
    return this.http.get<BranchDetail>(`${this.baseUrl}/${branchId}`, { observe: "response" })
      .pipe(map(response => this.versioned(response)));
  }

  create(request: BranchWriteRequest): Observable<VersionedBranch> {
    return this.http.post<BranchDetail>(this.baseUrl, request, { observe: "response" })
      .pipe(map(response => this.versioned(response)));
  }

  update(branchId: string, etag: string, request: BranchWriteRequest): Observable<VersionedBranch> {
    return this.http.put<BranchDetail>(`${this.baseUrl}/${branchId}`, request, {
      observe: "response",
      headers: this.match(etag)
    }).pipe(map(response => this.versioned(response)));
  }

  activate(branchId: string, etag: string): Observable<VersionedBranch> {
    return this.status(branchId, etag, "activate");
  }

  deactivate(branchId: string, etag: string): Observable<VersionedBranch> {
    return this.status(branchId, etag, "deactivate");
  }

  private status(branchId: string, etag: string, action: "activate" | "deactivate"): Observable<VersionedBranch> {
    return this.http.post<BranchDetail>(`${this.baseUrl}/${branchId}/${action}`, null, {
      observe: "response",
      headers: this.match(etag)
    }).pipe(map(response => this.versioned(response)));
  }

  private match(etag: string): HttpHeaders {
    return new HttpHeaders({ "If-Match": etag });
  }

  private versioned(response: HttpResponse<BranchDetail>): VersionedBranch {
    const etag = response.headers.get("ETag");
    if (!response.body || !etag) throw new Error("Branch response is incomplete.");
    return { branch: response.body, etag };
  }
}
