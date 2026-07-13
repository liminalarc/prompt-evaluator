import { test, expect } from '@playwright/test';

// Drives datasets end to end against the running stack (1.7): a dataset lives with a prompt, so
// it is created from the prompt's workspace, then captured/browsed on its own detail page.
// (Synthetic generation makes a live model call, so it is exercised by unit tests, not here.)
test('creates a dataset in a prompt workspace, captures a fixture, and filters by origin', async ({
  page,
}) => {
  const stamp = Date.now();
  const promptName = `e2e ds-owner ${stamp}`;
  const datasetName = `e2e dataset ${stamp}`;

  // An owning prompt, opened in its workspace.
  await page.goto('/prompts');
  await page.fill('#name', promptName);
  await page.getByTestId('create').click();
  await page.getByTestId('prompts').getByText(promptName).click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();

  // Create a dataset under the prompt, then open it.
  await page.fill('#datasetName', datasetName);
  await page.getByTestId('create-dataset').click();
  const dsRow = page.getByTestId('datasets').getByText(datasetName);
  await expect(dsRow).toBeVisible();
  await dsRow.click();

  await expect(page.getByRole('heading', { name: datasetName })).toBeVisible();

  // Capture a ground-truth fixture.
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
