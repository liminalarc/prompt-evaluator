import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { OrgRole } from '../users/user';
import { OrganizationAdmin, OrgMember } from './org-admin.model';

/**
 * The single API client for the admin organization-management surface (spec 4.4) —
 * `/api/admin/organizations`, global-admin gated on the server. Distinct from the member-scoped
 * {@link OrganizationsApiService} (the switcher / org creation). Components call this, never
 * HttpClient directly.
 */
@Injectable({ providedIn: 'root' })
export class OrgsAdminApiService {
  private readonly http = inject(HttpClient);

  /** Every organization with its member count. */
  listOrganizations(): Observable<OrganizationAdmin[]> {
    return this.http.get<OrganizationAdmin[]>('/api/admin/organizations');
  }

  createOrganization(name: string): Observable<OrganizationAdmin> {
    return this.http.post<OrganizationAdmin>('/api/admin/organizations', { name });
  }

  renameOrganization(id: string, name: string): Observable<OrganizationAdmin> {
    return this.http.put<OrganizationAdmin>(`/api/admin/organizations/${id}`, { name });
  }

  /** Deletes an org and everything under it — folders, prompts, datasets, runs cascade (1.10). */
  deleteOrganization(id: string): Observable<void> {
    return this.http.delete<void>(`/api/admin/organizations/${id}`);
  }

  listMembers(id: string): Observable<OrgMember[]> {
    return this.http.get<OrgMember[]>(`/api/admin/organizations/${id}/members`);
  }

  addMember(id: string, userId: string, role: OrgRole): Observable<void> {
    return this.http.post<void>(`/api/admin/organizations/${id}/members`, { userId, role });
  }

  removeMember(id: string, userId: string): Observable<void> {
    return this.http.delete<void>(`/api/admin/organizations/${id}/members/${userId}`);
  }
}
