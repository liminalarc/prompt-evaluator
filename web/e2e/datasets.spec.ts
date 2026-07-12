import { test, expect } from '@playwright/test';

// Drives datasets end to end against the running stack: create a dataset, capture a
// ground-truth fixture, browse it, and filter by origin — Angular -> API -> Postgres and back.
// (Synthetic generation makes a live model call, so it is exercised by unit tests, not here.)
test('creates a dataset, captures a fixture, and filters by origin', async ({ page }) => {
  const name = `e2e dataset ${Date.now()}`;

  await page.goto('/datasets');
  await page.fill('#name', name);
  await page.fill('#description', 'created by e2e');
  await page.getByTestId('create').click();

  // The new dataset appears in the list; open it.
  const row = page.getByTestId('datasets').getByText(name);
  await expect(row).toBeVisible();
  await row.click();

  await expect(page.getByRole('heading', { name })).toBeVisible();

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
