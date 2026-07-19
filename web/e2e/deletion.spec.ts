import { test, expect } from '@playwright/test';
import { selectOrg, createOrg, deleteOrg, orgName } from './support';

// Drives the 1.10 delete flows against the running stack: every destructive action is guarded by an
// in-app confirmation that states what cascades. Create a prompt then delete it (confirming), and
// delete the organization from the Prompts page (context repoints away). The org is deleted on
// teardown too — the endpoint is idempotent, so a double-delete is a harmless 204.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('deletes a prompt behind a confirmation, then deletes the organization', async ({
  page,
  request,
}) => {
  const stamp = Date.now();
  const promptName = `e2e del ${stamp}`;
  orgId = await createOrg(request, orgName('deletion'));

  await page.goto('/prompts');
  await selectOrg(page, orgId);

  // Create a prompt via the UI.
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  // Create-prompt lands on the new prompt's workspace (U1); return to the list to delete from there.
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();
  await page.goto('/prompts');
  await expect(page.getByTestId('prompts').getByRole('link', { name: promptName })).toBeVisible();

  // Delete it — a confirmation appears first and names what will be removed.
  const promptRow = page.getByTestId('prompts').locator('tr', { hasText: promptName });
  await promptRow.getByRole('button', { name: 'Delete' }).click();
  await expect(page.getByTestId('confirm-dialog')).toBeVisible();
  await expect(page.getByTestId('confirm-message')).toContainText(promptName);

  // Cancel leaves it in place…
  await page.getByTestId('confirm-cancel').click();
  await expect(page.getByTestId('confirm-dialog')).toHaveCount(0);
  await expect(page.getByTestId('prompts').getByRole('link', { name: promptName })).toBeVisible();

  // …confirm removes it.
  await promptRow.getByRole('button', { name: 'Delete' }).click();
  await page.getByTestId('confirm-delete').click();
  await expect(page.getByTestId('prompts').getByRole('link', { name: promptName })).toHaveCount(0);

  // Delete the whole organization from the Prompts page header, behind the same confirmation.
  await page.getByTestId('delete-org').click();
  await expect(page.getByTestId('confirm-dialog')).toBeVisible();
  await page.getByTestId('confirm-delete').click();

  // The deleted org drops out of the topbar switcher (context repointed away).
  await expect(page.locator(`[data-testid="org-option"][data-org-id="${orgId}"]`)).toHaveCount(0);
});
