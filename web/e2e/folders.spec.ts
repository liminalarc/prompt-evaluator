import { test, expect } from '@playwright/test';

// Drives org + folder navigation end to end against the running stack (1.9): create an org, add a
// folder, descend into it, file a prompt there, and confirm it's scoped to that folder.
test('creates an org and folder, files a prompt into it, and navigates by folder', async ({ page }) => {
  const stamp = Date.now();
  const org = `e2e org ${stamp}`;
  const folder = `e2e folder ${stamp}`;
  const prompt = `e2e filed ${stamp}`;

  await page.goto('/prompts');

  // Create an isolated organization and select it.
  await page.getByTestId('toggle-new-org').click();
  await page.fill('#orgName', org);
  await page.getByTestId('create-org').click();
  await expect(page.getByTestId('org-select')).toHaveValue(/.+/);

  // Create a top-level folder (at the org root), then descend into it.
  await page.getByTestId('toggle-new-folder').click();
  await page.fill('#folderName', folder);
  await page.getByTestId('create-folder').click();

  const card = page.getByTestId('subfolders').getByText(folder);
  await expect(card).toBeVisible();
  await card.click();
  await expect(page.getByTestId('breadcrumb')).toContainText(folder);

  // A prompt created while inside the folder is filed there.
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', prompt);
  await page.getByTestId('create-prompt').click();
  await expect(page.getByTestId('prompts').getByText(prompt)).toBeVisible();

  // Back at the org root (breadcrumb), the prompt is not listed — it's scoped to the folder.
  await page.getByTestId('breadcrumb').getByRole('button').first().click();
  await expect(page.getByTestId('prompts').getByText(prompt)).toHaveCount(0);
});
