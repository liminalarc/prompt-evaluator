import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Gate workspace-admin routes (spec 1.13). Resolves the session first, then allows only a global
 * admin through; everyone else is redirected to the dashboard. Layer after `authGuard` so an
 * unauthenticated user goes to /login rather than /.
 */
export const adminGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  await auth.ensureLoaded();

  if (auth.currentUser()?.isAdmin) {
    return true;
  }

  return router.createUrlTree(['/']);
};
