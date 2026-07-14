import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Organization } from '../organization';

/**
 * The single API client for the organizations bounded area (1.9) — the top-level container and
 * (later) permission boundary. Components call this, never HttpClient directly.
 */
@Injectable({ providedIn: 'root' })
export class OrganizationsApiService {
  private readonly http = inject(HttpClient);

  /** Every org one can access (access-filtered by 4.1 later; all for now) — the switcher list. */
  listOrganizations(): Observable<Organization[]> {
    return this.http.get<Organization[]>('/api/organizations');
  }

  createOrganization(name: string): Observable<Organization> {
    return this.http.post<Organization>('/api/organizations', { name });
  }

  renameOrganization(id: string, name: string): Observable<Organization> {
    return this.http.put<Organization>(`/api/organizations/${id}`, { name });
  }
}
