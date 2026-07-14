import { test as setup, expect } from '@playwright/test';
import { authFile } from './support';

// Authenticate once for the whole e2e run (4.1). Every app route is now behind `authGuard`, so the
// pre-existing specs need a signed-in session to reach the screens they exercise. This setup
// project registers ONE fresh user via the API — which auto-signs-in and sets the HttpOnly auth
// cookie — then saves the resulting storageState. The `chromium` project depends on this and loads
// the state, so both the browser context AND the test-scoped `request` fixture start authenticated.
//
// A fresh user has no organizations (registration self-provisions an account with none), which is
// exactly what the specs assume: each creates the disposable org(s) it needs. The email is stamped
// per run so re-runs never collide on a duplicate registration.
setup('authenticate', async ({ request }) => {
  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
  const res = await request.post('/api/auth/register', {
    data: {
      email: `e2e-shared-${stamp}@example.com`,
      displayName: `E2E Shared ${stamp}`,
      password: 'Correct-Horse-9',
    },
  });
  expect(res.ok()).toBeTruthy();

  // Persist the auth cookie for the dependent project(s) to reuse.
  await request.storageState({ path: authFile });
});
