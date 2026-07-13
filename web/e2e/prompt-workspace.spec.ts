import { test, expect } from '@playwright/test';

// Drives the unified prompt workspace (1.7): a prompt's versions, its datasets, and its analytics
// all live on one page. Angular -> API -> Postgres and back.
test('a prompt workspace shows its versions, datasets, and analytics together', async ({ page }) => {
  const stamp = Date.now();
  const promptName = `e2e workspace ${stamp}`;
  const datasetName = `e2e ws-data ${stamp}`;

  await page.goto('/prompts');
  await page.fill('#name', promptName);
  await page.getByTestId('create').click();
  await page.getByTestId('prompts').getByText(promptName).click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();

  // Versions live here.
  await page.fill('#content', 'Summarize: {input}');
  await page.fill('#targetModel', 'claude-sonnet-5');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions')).toContainText('claude-sonnet-5');

  // Datasets live here too — create one under the prompt.
  await page.fill('#datasetName', datasetName);
  await page.getByTestId('create-dataset').click();
  await expect(page.getByTestId('datasets')).toContainText(datasetName);

  // Analytics live here as well: pick the dataset; with no runs yet the chart shows its empty state.
  await page.getByTestId('analytics-dataset').selectOption({ label: datasetName });
  await expect(page.getByTestId('chart-empty')).toBeVisible();
});
