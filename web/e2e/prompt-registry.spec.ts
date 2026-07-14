import { test, expect } from '@playwright/test';

// Drives the registry end to end against the running stack: create a prompt, add two
// versions, view its history, and compare versions — Angular -> API -> Postgres and back.
test('registers a prompt, records versions, and diffs them', async ({ page }) => {
  const name = `e2e prompt ${Date.now()}`;

  await page.goto('/prompts');
  // Org dropdown defaults to the seeded Default org; reveal the collapsed new-prompt form.
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', name);
  await page.fill('#description', 'created by e2e');
  await page.getByTestId('create-prompt').click();

  // The new prompt appears in the list; open it.
  const row = page.getByTestId('prompts').getByText(name);
  await expect(row).toBeVisible();
  await row.click();

  await expect(page.getByRole('heading', { name })).toBeVisible();

  // Add first version.
  await page.fill('#content', 'Summarize: {input}');
  await page.fill('#targetModel', 'claude-sonnet-5');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions')).toContainText('claude-sonnet-5');

  // Add second version.
  await page.fill('#content', 'Summarize concisely: {input}');
  await page.fill('#targetModel', 'claude-opus-4-8');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions')).toContainText('claude-opus-4-8');

  // The diff between the two versions is shown.
  const diff = page.getByTestId('diff');
  await expect(diff).toBeVisible();
  await expect(diff).toContainText('+ Summarize concisely: {input}');
});
