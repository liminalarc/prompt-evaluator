import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Organization } from '../organization';
import { OrgRole } from '../users/user';
import { OrgMember } from './org-admin.model';

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

  /** Deletes an org and everything under it — folders, prompts, datasets cascade (1.9/1.10). */
  deleteOrganization(id: string): Observable<void> {
    return this.http.delete<void>(`/api/organizations/${id}`);
  }

  // --- Owner-facing member management (4.5): owner-or-admin gated on the server. ---

  /** The members of an org with their roles — the owner-facing member view. */
  listMembers(id: string): Observable<OrgMember[]> {
    return this.http.get<OrgMember[]>(`/api/organizations/${id}/members`);
  }

  /** Add (or upgrade) a member by email — owners can't enumerate the admin-gated user directory. */
  addMemberByEmail(id: string, email: string, role: OrgRole): Observable<void> {
    return this.http.post<void>(`/api/organizations/${id}/members`, { email, role });
  }

  /** Change an existing member's role. Blocked server-side if it would strand the org with no owner. */
  setMemberRole(id: string, userId: string, role: OrgRole): Observable<void> {
    return this.http.put<void>(`/api/organizations/${id}/members/${userId}`, { role });
  }

  /** Remove a member. Blocked server-side if they are the org's last owner. */
  removeMember(id: string, userId: string): Observable<void> {
    return this.http.delete<void>(`/api/organizations/${id}/members/${userId}`);
  }
}
