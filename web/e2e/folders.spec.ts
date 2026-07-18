import { test, expect } from '@playwright/test';
import { deleteOrg } from './support';

// Drives org + folder navigation end to end against the running stack (1.9): create an org, add a
// folder, descend into it, file a prompt there, and confirm it's scoped to that folder. The org is
// created through the UI (covering that flow) and deleted on teardown so nothing is left behind.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('creates an org and folder, files a prompt into it, and navigates by folder', async ({
  page,
}) => {
  const stamp = Date.now();
  const org = `e2e org ${stamp}`;
  const folder = `e2e folder ${stamp}`;
  const prompt = `e2e filed ${stamp}`;

  await page.goto('/prompts');

  // Create an isolated organization via the UI and select it; capture its id from the create
  // response so teardown can delete it reliably.
  await page.getByTestId('toggle-new-org').click();
  await page.fill('#orgName', org);
  const [orgRes] = await Promise.all([
    page.waitForResponse(
      (r) => r.url().endsWith('/api/organizations') && r.request().method() === 'POST',
    ),
    page.getByTestId('create-org').click(),
  ]);
  orgId = (await orgRes.json()).id;
  await expect(page.getByTestId('org-select')).toHaveValue(orgId);

  // Create a top-level folder (at the org root), then descend into it.
  await page.getByTestId('toggle-new-folder').click();
  await page.fill('#folderName', folder);
  await page.getByTestId('create-folder').click();

  const card = page.getByTestId('subfolders').getByText(folder);
  await expect(card).toBeVisible();
  await card.click();
  await expect(page.getByTestId('breadcrumb')).toContainText(folder);

  // A prompt created while inside the folder is filed there; create-prompt lands on its workspace (U1).
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', prompt);
  await page.getByTestId('create-prompt').click();
  await expect(page.getByRole('heading', { name: prompt })).toBeVisible();

  // Back at the org root, the prompt is not listed — it's scoped to the folder.
  await page.goto('/prompts');
  await expect(page.getByTestId('prompts').getByText(prompt)).toHaveCount(0);

  // Descend into the folder again — the prompt is filed there.
  await page.getByTestId('subfolders').getByText(folder).click();
  await expect(page.getByTestId('prompts').getByText(prompt)).toBeVisible();
});
