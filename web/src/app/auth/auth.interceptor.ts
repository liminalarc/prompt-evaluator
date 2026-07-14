import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Requests to the auth endpoints own their own 401s (bad login, unauthenticated `/me`). */
function isAuthEndpoint(url: string): boolean {
  return url.includes('/api/auth/');
}

/**
 * Cookie auth (4.1): stamp `withCredentials` on every request so the same-origin HttpOnly auth
 * cookie is sent, and treat an unexpected 401 (session expired / lost) as "signed out" — clear
 * the session and bounce to `/login`. Auth-endpoint 401s are left to their callers (a failed
 * login shows a form error; the bootstrap `/me` probe just means "not signed in").
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const withCreds = req.clone({ withCredentials: true });

  return next(withCreds).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isAuthEndpoint(withCreds.url)) {
        auth.clearSession();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
  );
};
