import { test, expect } from '@playwright/test';
import { selectOrg, createOrg, deleteOrg, orgName } from './support';

// Drives datasets end to end against the running stack (1.7/1.9): a dataset lives with a prompt in
// an org, so it is created from the prompt's workspace, then captured/browsed on its own detail
// page. Runs inside a disposable org deleted on teardown. (Synthetic generation makes a live model
// call, so it is exercised by unit tests, not here.)
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('creates a dataset in a prompt workspace, captures a fixture, and filters by origin', async ({
  page,
  request,
}) => {
  const stamp = Date.now();
  const promptName = `e2e ds-owner ${stamp}`;
  const datasetName = `e2e dataset ${stamp}`;
  orgId = await createOrg(request, orgName('datasets'));

  // An owning prompt in the disposable org, opened in its workspace.
  await page.goto('/prompts');
  await selectOrg(page, orgId);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  // Create-prompt lands on the new prompt's workspace (U1).
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();

  // Create a dataset under the prompt (Datasets tab, 2.19 D2), then open it.
  await page.getByTestId('tab-datasets').click();
  await page.getByTestId('toggle-create-dataset').click();
  await page.fill('#datasetName', datasetName);
  await page.getByTestId('create-dataset').click();
  const dsRow = page.getByTestId('datasets').getByText(datasetName);
  await expect(dsRow).toBeVisible();
  await dsRow.click();

  await expect(page.getByRole('heading', { name: datasetName })).toBeVisible();

  // Capture a ground-truth fixture.
  await page.getByTestId('toggle-capture').click();
  await page.fill('#promptInput', 'summarize this captured thread');
  await page.fill('#slmOutput', 'raw upstream slm output');
  await page.getByTestId('capture').click();

  const fixtures = page.getByTestId('fixtures');
  await expect(fixtures).toContainText('summarize this captured thread');
  await expect(fixtures.locator('tr[data-origin="Captured"]')).toHaveCount(1);

  // Filter to Synthetic — the captured fixture drops out.
  await page.getByTestId('origin-filter').selectOption('Synthetic');
  await expect(page.getByTestId('no-fixtures')).toBeVisible();

  // Filter back to Captured — it returns.
  await page.getByTestId('origin-filter').selectOption('Captured');
  await expect(page.getByTestId('fixtures').locator('tr[data-origin="Captured"]')).toHaveCount(1);
});
