import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { signal } from '@angular/core';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

/** A signals-based fake AuthService whose `ensureLoaded` resolves the given authenticated state. */
function fakeAuth(authenticatedAfterLoad: boolean) {
  const authed = signal(false);
  return {
    isAuthenticated: () => authed(),
    ensureLoaded: async () => {
      authed.set(authenticatedAfterLoad);
    },
  };
}

function run(auth: ReturnType<typeof fakeAuth>, url = '/prompts') {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
  });
  const state = { url } as RouterStateSnapshot;
  const route = {} as ActivatedRouteSnapshot;
  return TestBed.runInInjectionContext(() => authGuard(route, state));
}

describe('authGuard', () => {
  it('awaits the session load and allows an authenticated user through', async () => {
    const result = await run(fakeAuth(true));
    expect(result).toBeTrue();
  });

  it('redirects an unauthenticated user to /login with a returnUrl', async () => {
    const result = (await run(fakeAuth(false), '/prompts/abc')) as UrlTree;
    const router = TestBed.inject(Router);
    const expected = router.createUrlTree(['/login'], {
      queryParams: { returnUrl: '/prompts/abc' },
    });
    expect(result.toString()).toBe(expected.toString());
  });
});
