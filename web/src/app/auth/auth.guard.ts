import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Gate the authenticated app (4.1). Ensures the session is resolved first (awaits `/me` if
 * bootstrap hasn't already), then allows an authenticated user through or redirects to `/login`
 * carrying a `returnUrl` so the login flow can send them back where they were headed.
 */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  await auth.ensureLoaded();

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
