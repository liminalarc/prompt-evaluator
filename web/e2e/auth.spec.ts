import { test, expect } from '@playwright/test';

// Auth happy-path (4.1): register a brand-new user → land in the app authenticated → logout →
// bounced to /login. Registration self-provisions an account with no org access, so a fresh user
// simply has no organizations (the switcher stays hidden) — that's the state we assert.
//
// This needs the full stack (API + identity store). The normal e2e run points at a running
// compose stack; if the API isn't reachable we skip rather than fail, matching how the
// model-dependent specs self-skip (see datasets-generate.spec.ts and web/CLAUDE.md).
test('register a new user, land authenticated, then logout', async ({ page, request }) => {
  const probe = await request.get('/api/auth/me').catch(() => null);
  test.skip(!probe, 'requires a running stack (API unreachable)');

  const stamp = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
  const email = `e2e-auth-${stamp}@example.com`;
  const displayName = `E2E User ${stamp}`;
  const password = 'Sup3r-Secret-Pw!';

  // Visiting a guarded route unauthenticated redirects to /login.
  await page.goto('/');
  await expect(page).toHaveURL(/\/login/);

  // Go to register and create the account (auto-signs-in, sets the cookie).
  await page.getByTestId('to-register').click();
  await expect(page.getByTestId('register-page')).toBeVisible();
  await page.fill('#email', email);
  await page.fill('#displayName', displayName);
  await page.fill('#password', password);
  await page.getByTestId('register-submit').click();

  // Landed in the authenticated app: not on /login, and the shell shows the signed-in user.
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page.getByTestId('current-user')).toContainText(displayName);
  await expect(page.getByTestId('logout')).toBeVisible();

  // Logout → bounced back to /login, user chrome gone.
  await page.getByTestId('logout').click();
  await expect(page).toHaveURL(/\/login/);
  await expect(page.getByTestId('logout')).toHaveCount(0);

  // The guard still protects the app after logout.
  await page.goto('/prompts');
  await expect(page).toHaveURL(/\/login/);
});
