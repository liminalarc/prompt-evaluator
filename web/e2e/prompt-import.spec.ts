import { test, expect } from '@playwright/test';
import { createOrg, deleteOrg, orgName } from './support';

// Single-file import (1.6): a prompt owner picks a text file in the add-version form and its
// contents load into the version, then the unchanged add-version POST copies it in. Runs inside a
// disposable org deleted on teardown.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('imports a version’s content from a picked text file', async ({ page, request }) => {
  const stamp = Date.now();
  const promptName = `e2e import ${stamp}`;
  orgId = await createOrg(request, orgName('import'));

  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgId);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  await page.getByTestId('prompts').getByRole('link', { name: promptName }).click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();

  await page.getByTestId('toggle-add-version').click();
  // Pick an in-memory text file; its contents flow into the content textarea via FileReader.
  await page.getByTestId('import-version-file').setInputFiles({
    name: 'imported.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('Imported prompt: {input}'),
  });
  await expect(page.locator('#content')).toHaveValue('Imported prompt: {input}');

  await page.fill('#targetModel', 'claude-sonnet-5');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions')).toContainText('claude-sonnet-5');
});

test('bulk-imports several prompts from a JSON file with a per-row report', async ({
  page,
  request,
}) => {
  const stamp = Date.now();
  const alpha = `e2e bulk alpha ${stamp}`;
  const beta = `e2e bulk beta ${stamp}`;
  orgId = await createOrg(request, orgName('bulk'));

  const payload = JSON.stringify([
    { name: alpha, versions: [{ content: 'Alpha: {input}', targetModel: 'claude-sonnet-5' }] },
    { name: beta },
  ]);

  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgId);
  await page.getByTestId('toggle-import').click();
  await page.getByTestId('import-file').setInputFiles({
    name: 'prompts.json',
    mimeType: 'application/json',
    buffer: Buffer.from(payload),
  });

  // Per-row report shows both prompts succeeded…
  await expect(page.getByTestId('import-results')).toContainText(alpha);
  await expect(page.getByTestId('import-results')).toContainText(beta);
  await expect(page.getByTestId('import-ok')).toHaveCount(2);

  // …and both now appear in the current folder's prompt list.
  await expect(page.getByTestId('prompts')).toContainText(alpha);
  await expect(page.getByTestId('prompts')).toContainText(beta);
});
