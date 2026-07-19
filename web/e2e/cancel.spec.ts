import { test, expect } from '@playwright/test';
import { selectOrg, createOrg, deleteOrg, orgName } from './support';

// Drives the 2.11 Cancel affordance end to end: opening a reveal form, typing, then Cancel must
// collapse the form and discard the unsaved input — nothing is persisted and the row count holds.
// Representative surface is add-fixture (the spec's e2e acceptance case). Runs in a disposable org.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('Cancel on add-fixture discards input and leaves the fixture count unchanged [2.11]', async ({
  page,
  request,
}) => {
  const stamp = Date.now();
  const promptName = `e2e cancel-owner ${stamp}`;
  const datasetName = `e2e cancel dataset ${stamp}`;
  orgId = await createOrg(request, orgName('cancel'));

  // An owning prompt + dataset in the disposable org.
  await page.goto('/prompts');
  await selectOrg(page, orgId);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();

  await page.getByTestId('tab-datasets').click();
  await page.getByTestId('toggle-create-dataset').click();
  await page.fill('#datasetName', datasetName);
  await page.getByTestId('create-dataset').click();
  const dsRow = page.getByTestId('datasets').getByText(datasetName);
  await expect(dsRow).toBeVisible();
  await dsRow.click();
  await expect(page.getByRole('heading', { name: datasetName })).toBeVisible();

  // The dataset starts with no fixtures.
  await expect(page.getByTestId('no-fixtures')).toBeVisible();

  // Open add-fixture, type into it, then Cancel.
  await page.getByTestId('toggle-capture').click();
  await page.fill('#promptInput', 'this input should be discarded');
  await page.getByTestId('cancel-capture').click();

  // The form collapsed (submit gone) and nothing was captured — still zero fixtures.
  await expect(page.getByTestId('capture')).toHaveCount(0);
  await expect(page.getByTestId('no-fixtures')).toBeVisible();

  // Re-opening the form shows the field cleared (unsaved input was discarded, not retained).
  await page.getByTestId('toggle-capture').click();
  await expect(page.locator('#promptInput')).toHaveValue('');
});
