import { test, expect } from '@playwright/test';
import { createOrg, deleteOrg, orgName } from './support';

// Drives the unified prompt workspace (1.7/1.9): a prompt's versions, its datasets, and its
// analytics all live on one page. Runs inside a disposable org deleted on teardown.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('a prompt workspace shows its versions, datasets, and analytics together', async ({
  page,
  request,
}) => {
  const stamp = Date.now();
  const promptName = `e2e workspace ${stamp}`;
  const datasetName = `e2e ws-data ${stamp}`;
  orgId = await createOrg(request, orgName('workspace'));

  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgId);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  // Create-prompt lands on the new prompt's workspace (U1).
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();

  // Versions live here.
  await page.getByTestId('toggle-add-version').click();
  await page.fill('#content', 'Summarize: {input}');
  await page.selectOption('#targetModel', 'claude-sonnet-5');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions')).toContainText('claude-sonnet-5');

  // R5 — a new version defaults its Target model to the last version's (holds it), and warns on a
  // change. The add-version form stays open after a submit, so close it then re-open it fresh: it
  // should default (hold) v1's model, with no warning yet.
  await page.getByTestId('cancel-add-version').click();
  await page.getByTestId('toggle-add-version').click();
  await expect(page.locator('#targetModel')).toHaveValue('claude-sonnet-5');
  await expect(page.getByTestId('model-change-warning')).toHaveCount(0);
  // Switch the subject model → the drift warning appears; switch back → it clears.
  await page.selectOption('#targetModel', 'claude-opus-4-8');
  await expect(page.getByTestId('model-change-warning')).toBeVisible();
  await page.selectOption('#targetModel', 'claude-sonnet-5');
  await expect(page.getByTestId('model-change-warning')).toHaveCount(0);
  await page.getByTestId('cancel-add-version').click();

  // Datasets live here too — create one under the prompt.
  await page.getByTestId('toggle-create-dataset').click();
  await page.fill('#datasetName', datasetName);
  await page.getByTestId('create-dataset').click();
  await expect(page.getByTestId('datasets')).toContainText(datasetName);

  // Analytics live here as well: pick the dataset; with no runs yet the chart shows its empty state.
  await page.getByTestId('analytics-dataset').selectOption({ label: datasetName });
  await expect(page.getByTestId('chart-empty')).toBeVisible();
});
