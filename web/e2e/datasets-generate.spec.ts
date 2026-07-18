import { test, expect } from '@playwright/test';
import { createOrg, deleteOrg, orgName } from './support';

// Exercises the in-browser "Generate" flow end to end. A dataset lives with a prompt (1.7/1.9), so
// it's created from the prompt's workspace, then generated on its own page. Synthetic generation
// normally makes a live Claude call, so this runs only against the stubbed eval-runner:
//   docker compose -f docker-compose.yml -f docker-compose.e2e.yml up -d --build
//   E2E_EVAL_RUNNER_STUB=1 npx playwright test e2e/datasets-generate.spec.ts
// Skipped by default so the normal e2e run never hits the model. Runs in a disposable org.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('generates synthetic fixtures from a captured seed', async ({ page, request }) => {
  test.skip(
    !process.env['E2E_EVAL_RUNNER_STUB'],
    'requires the stubbed eval-runner stack (docker-compose.e2e.yml)',
  );

  const stamp = Date.now();
  const promptName = `e2e generate prompt ${stamp}`;
  const datasetName = `e2e generate ${stamp}`;
  orgId = await createOrg(request, orgName('generate'));

  // Create a prompt in the disposable org, then a dataset under it, and open the dataset.
  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgId);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  // Create-prompt lands on the new prompt's workspace (U1).
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();
  await page.getByTestId('toggle-create-dataset').click();
  await page.fill('#datasetName', datasetName);
  await page.getByTestId('create-dataset').click();
  await page.getByTestId('datasets').getByRole('link', { name: datasetName }).click();
  await expect(page.getByRole('heading', { name: datasetName })).toBeVisible();

  // A captured fixture is required to seed generation.
  await page.getByTestId('toggle-capture').click();
  await page.fill('#promptInput', 'summarize this captured thread');
  await page.getByTestId('capture').click();
  await expect(page.getByTestId('fixtures').locator('tr[data-origin="Captured"]')).toHaveCount(1);

  // Trigger generation; the stub returns deterministic synthetic fixtures.
  await page.getByTestId('toggle-generate').click();
  await page.fill('#count', '2');
  await page.getByTestId('generate').click();

  const synthetic = page.getByTestId('fixtures').locator('tr[data-origin="Synthetic"]');
  await expect(synthetic).toHaveCount(2);
  await expect(synthetic.first()).toContainText('[synthetic] summarize this captured thread');

  // Origin filter narrows to just the generated rows.
  await page.getByTestId('origin-filter').selectOption('Synthetic');
  await expect(page.getByTestId('fixtures').locator('tbody tr')).toHaveCount(2);
});
