import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { Observable, catchError, firstValueFrom, of, tap } from 'rxjs';
import { AuthUser } from './user';

/**
 * The single API client + session state for the Identity bounded context (4.1). Cookie-based:
 * the HttpOnly auth cookie is set/cleared by the API and travels same-origin (dev proxy /
 * compose nginx), so every call just needs `withCredentials` (the interceptor adds it globally;
 * we also set it here so the service is correct on its own).
 *
 * `currentUser` is the source of truth for "who is signed in"; guards + the shell read it.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _currentUser = signal<AuthUser | null>(null);
  readonly currentUser: Signal<AuthUser | null> = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this._currentUser() !== null);

  /** True once `/me` has been resolved at least once (bootstrap or a guard) — see `ensureLoaded`. */
  private sessionLoaded = false;

  /**
   * Resolve the current session from the cookie. Settles (never rejects) so bootstrap +
   * guards can `await` it: 200 → sets the user, 401 → clears it. Marks the session loaded.
   */
  loadSession(): Promise<AuthUser | null> {
    return firstValueFrom(
      this.http.get<AuthUser>('/api/auth/me', { withCredentials: true }).pipe(
        tap((user) => this._currentUser.set(user)),
        catchError(() => {
          this._currentUser.set(null);
          return of(null);
        }),
      ),
    ).finally(() => {
      this.sessionLoaded = true;
    });
  }

  /** Load the session once (used by the guard); no-op if bootstrap already resolved it. */
  async ensureLoaded(): Promise<void> {
    if (!this.sessionLoaded) {
      await this.loadSession();
    }
  }

  login(email: string, password: string): Observable<AuthUser> {
    return this.http
      .post<AuthUser>('/api/auth/login', { email, password }, { withCredentials: true })
      .pipe(tap((user) => this._currentUser.set(user)));
  }

  register(email: string, displayName: string, password: string): Observable<AuthUser> {
    return this.http
      .post<AuthUser>(
        '/api/auth/register',
        { email, displayName, password },
        { withCredentials: true },
      )
      .pipe(tap((user) => this._currentUser.set(user)));
  }

  logout(): Observable<void> {
    return this.http
      .post<void>('/api/auth/logout', {}, { withCredentials: true })
      .pipe(tap(() => this._currentUser.set(null)));
  }

  /** Self-service change-password (4.3): current + new, no email. */
  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(
      '/api/auth/change-password',
      { currentPassword, newPassword },
      { withCredentials: true },
    );
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<void>('/api/auth/forgot-password', { email }, { withCredentials: true });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.http.post<void>(
      '/api/auth/reset-password',
      { email, token, newPassword },
      { withCredentials: true },
    );
  }

  /** Drop the in-memory session (the interceptor calls this on an unexpected 401). */
  clearSession(): void {
    this._currentUser.set(null);
  }
}
