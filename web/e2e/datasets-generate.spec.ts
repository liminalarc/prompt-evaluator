import { test, expect } from '@playwright/test';

// Exercises the in-browser "Generate" flow end to end. Synthetic generation normally makes a
// live Claude call, so this runs only against the stubbed eval-runner (deterministic, no key):
//   docker compose -f docker-compose.yml -f docker-compose.e2e.yml up -d --build
//   E2E_EVAL_RUNNER_STUB=1 npx playwright test e2e/datasets-generate.spec.ts
// It is skipped by default so the normal e2e run never hits the model.
test('generates synthetic fixtures from a captured seed', async ({ page }) => {
  test.skip(
    !process.env['E2E_EVAL_RUNNER_STUB'],
    'requires the stubbed eval-runner stack (docker-compose.e2e.yml)',
  );

  const name = `e2e generate ${Date.now()}`;

  await page.goto('/datasets');
  await page.fill('#name', name);
  await page.getByTestId('create').click();
  await page.getByTestId('datasets').getByText(name).click();
  await expect(page.getByRole('heading', { name })).toBeVisible();

  // A captured fixture is required to seed generation.
  await page.fill('#promptInput', 'summarize this captured thread');
  await page.getByTestId('capture').click();
  await expect(page.getByTestId('fixtures').locator('tr[data-origin="Captured"]')).toHaveCount(1);

  // Trigger generation; the stub returns deterministic synthetic fixtures.
  await page.fill('#count', '2');
  await page.getByTestId('generate').click();

  const synthetic = page.getByTestId('fixtures').locator('tr[data-origin="Synthetic"]');
  await expect(synthetic).toHaveCount(2);
  await expect(synthetic.first()).toContainText('[synthetic] summarize this captured thread');

  // Origin filter narrows to just the generated rows.
  await page.getByTestId('origin-filter').selectOption('Synthetic');
  await expect(page.getByTestId('fixtures').locator('tbody tr')).toHaveCount(2);
});
