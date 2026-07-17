import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { OrgRole, UserDetail } from './user';

/**
 * The single API client for admin user & access management (spec 4.3). Global-admin gated on the
 * server; components call this, never HttpClient directly.
 */
@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly http = inject(HttpClient);

  listUsers(): Observable<UserDetail[]> {
    return this.http.get<UserDetail[]>('/api/admin/users');
  }

  setAdmin(id: string, isAdmin: boolean): Observable<void> {
    return this.http.post<void>(`/api/admin/users/${id}/admin`, { isAdmin });
  }

  grantMembership(id: string, organizationId: string, role: OrgRole): Observable<void> {
    return this.http.post<void>(`/api/admin/users/${id}/organizations`, { organizationId, role });
  }

  revokeMembership(id: string, organizationId: string): Observable<void> {
    return this.http.delete<void>(`/api/admin/users/${id}/organizations/${organizationId}`);
  }

  setPassword(id: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`/api/admin/users/${id}/password`, { newPassword });
  }
}
