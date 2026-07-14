import { test, expect } from '@playwright/test';
import { createOrg, deleteOrg, orgName } from './support';

// The coherent app shell (2.4): the dashboard is the landing page, the topbar is
// Dashboard · Prompts · Analytics (Datasets demoted), and detail pages carry a consistent
// breadcrumb that navigates back. No model needed — this is pure navigation/IA. Disposable org.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('dashboard landing, topbar IA, and breadcrumb navigation form a coherent loop', async ({
  page,
  request,
}) => {
  const stamp = Date.now();
  const promptName = `nav prompt ${stamp}`;
  orgId = await createOrg(request, orgName('nav'));

  // A prompt in the org.
  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgId);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  await expect(page.getByTestId('prompts').getByRole('link', { name: promptName })).toBeVisible();

  // Topbar IA: Dashboard/Prompts/Analytics present; Datasets demoted off the primary nav.
  const nav = page.locator('.nav');
  await expect(nav.getByRole('link', { name: 'Dashboard' })).toBeVisible();
  await expect(nav.getByRole('link', { name: 'Analytics' })).toBeVisible();
  await expect(nav.getByRole('link', { name: 'Datasets' })).toHaveCount(0);

  // The dashboard is the landing page and surfaces the org's prompt as a card.
  await nav.getByRole('link', { name: 'Dashboard' }).click();
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
  const card = page.getByTestId('dash-prompt-card').filter({ hasText: promptName });
  await expect(card).toBeVisible();

  // Into the workspace; the breadcrumb reflects the trail and navigates back.
  await card.click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();
  const breadcrumb = page.getByTestId('breadcrumb');
  await expect(breadcrumb).toContainText('Dashboard');
  await expect(breadcrumb).toContainText('Prompts');
  await breadcrumb.getByRole('link', { name: 'Prompts' }).click();
  await expect(page).toHaveURL(/\/prompts(\?|$)/);
});
