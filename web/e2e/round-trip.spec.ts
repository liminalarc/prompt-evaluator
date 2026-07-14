import { test, expect } from '@playwright/test';

test('round-trips a prompt through every layer', async ({ page }) => {
  // The echo skeleton is no longer the landing page (2.4) — it lives at /_skeleton purely as a
  // wiring smoke test proving the Angular → API → eval-runner seam.
  await page.goto('/_skeleton');

  const prompt = `e2e ${Date.now()}`;
  await page.fill('#prompt', prompt);
  await page.getByTestId('run').click();

  const result = page.getByTestId('result');
  await expect(result).toBeVisible();
  // The output is the eval-runner echo; its presence proves the Angular -> API ->
  // eval-runner seam is wired end-to-end.
  await expect(result).toContainText(prompt);
});
