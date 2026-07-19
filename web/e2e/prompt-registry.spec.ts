import { test, expect } from '@playwright/test';
import { createOrg, deleteOrg, orgName } from './support';

// Drives the registry end to end against the running stack: create a prompt, add two
// versions, view its history, and compare versions — Angular -> API -> Postgres and back.
// Works inside a disposable org that is deleted on teardown so no data is left behind.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('registers a prompt, records versions, and diffs them', async ({ page, request }) => {
  const name = `e2e prompt ${Date.now()}`;
  orgId = await createOrg(request, orgName('registry'));

  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgId);
  // Reveal the collapsed new-prompt form.
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', name);
  await page.fill('#description', 'created by e2e');
  await page.getByTestId('create-prompt').click();

  // Create-prompt lands on the new prompt's workspace (U1).
  await expect(page.getByRole('heading', { name })).toBeVisible();

  // Add first version.
  await page.getByTestId('toggle-add-version').click();
  await page.fill('#content', 'Summarize: {input}');
  await page.selectOption('#targetModel', 'claude-sonnet-5');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions')).toContainText('claude-sonnet-5');

  // Add second version.
  await page.fill('#content', 'Summarize concisely: {input}');
  await page.selectOption('#targetModel', 'claude-opus-4-8');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions')).toContainText('claude-opus-4-8');

  // Compare is the unified drawer now (2.19 W7): open it; the Content tab (default) diffs the two
  // latest versions.
  await page.getByTestId('open-compare').click();
  await expect(page.getByTestId('drawer')).toBeVisible();
  const diff = page.getByTestId('diff');
  await expect(diff).toBeVisible();
  await expect(diff).toContainText('+ Summarize concisely: {input}');
});
