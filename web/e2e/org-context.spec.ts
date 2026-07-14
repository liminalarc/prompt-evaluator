import { test, expect } from '@playwright/test';
import { createOrg, deleteOrg, orgName } from './support';

// The global org context (2.4): the topbar switcher scopes every surface, and the selection
// persists across navigation + reload (localStorage + ?org=). Two disposable orgs, each with its
// own prompt; switching the topbar org must swap what prompts/analytics show. Deleted on teardown.
let orgA = '';
let orgB = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgA);
  await deleteOrg(request, orgB);
  orgA = '';
  orgB = '';
});

test('the topbar org switcher scopes prompts and analytics and persists across navigation', async ({
  page,
  request,
}) => {
  const stamp = Date.now();
  const alpha = `alpha ${stamp}`;
  const beta = `beta ${stamp}`;
  orgA = await createOrg(request, orgName('org-ctx-A'));
  orgB = await createOrg(request, orgName('org-ctx-B'));

  // A prompt in org A.
  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgA);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', alpha);
  await page.getByTestId('create-prompt').click();
  await expect(page.getByTestId('prompts').getByRole('link', { name: alpha })).toBeVisible();
  await expect(page).toHaveURL(/[?&]org=/); // selection reflected in the url

  // A prompt in org B.
  await page.getByTestId('org-select').selectOption(orgB);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', beta);
  await page.getByTestId('create-prompt').click();
  await expect(page.getByTestId('prompts').getByRole('link', { name: beta })).toBeVisible();
  // org A's prompt is no longer listed — the list is scoped to the active org
  await expect(page.getByTestId('prompts').getByRole('link', { name: alpha })).toHaveCount(0);

  // Switch back to A — its prompt returns, B's drops out.
  await page.getByTestId('org-select').selectOption(orgA);
  await expect(page.getByTestId('prompts').getByRole('link', { name: alpha })).toBeVisible();
  await expect(page.getByTestId('prompts').getByRole('link', { name: beta })).toHaveCount(0);

  // The selection survives a hard navigation (persisted) and scopes analytics too.
  await page.goto('/analytics');
  await expect(page.getByTestId('org-select')).toHaveValue(orgA);
  await expect(page.getByTestId('prompt-select')).toContainText(alpha);
  await expect(page.getByTestId('prompt-select')).not.toContainText(beta);
});
