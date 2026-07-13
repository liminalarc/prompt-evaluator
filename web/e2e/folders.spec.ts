import { test, expect } from '@playwright/test';

// Drives folder browsing end to end against the running stack (1.7): create a folder, file a
// prompt into it, confirm it's scoped to that folder, then move it back to the root.
test('creates a folder, files a prompt into it, and moves it back to root', async ({ page }) => {
  const stamp = Date.now();
  const folder = `e2e folder ${stamp}`;
  const prompt = `e2e filed ${stamp}`;

  await page.goto('/prompts');

  // Create a top-level folder (Root is selected by default).
  await page.getByTestId('folder-name').fill(folder);
  await page.getByTestId('create-folder').click();

  // Select it in the tree; the breadcrumb reflects the path.
  await page.getByTestId('folder-tree').getByText(folder, { exact: true }).click();
  await expect(page.getByTestId('breadcrumb')).toContainText(`Root / ${folder}`);

  // A prompt created while the folder is selected lands in that folder.
  await page.fill('#name', prompt);
  await page.getByTestId('create').click();
  await expect(page.getByTestId('prompts').getByText(prompt)).toBeVisible();

  // It is scoped to the folder — not visible under Root.
  await page.getByTestId('folder-root').click();
  await expect(page.getByTestId('prompts').getByText(prompt)).toHaveCount(0);

  // Move it back to Root via its row select, then confirm it shows under Root.
  await page.getByTestId('folder-tree').getByText(folder, { exact: true }).click();
  const row = page.getByTestId('prompts').locator('tr', { hasText: prompt });
  await row.locator('select').selectOption({ label: 'Root' });

  await page.getByTestId('folder-root').click();
  await expect(page.getByTestId('prompts').getByText(prompt)).toBeVisible();
});
