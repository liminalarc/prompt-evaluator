import { test, expect } from '@playwright/test';
import { createOrg, deleteOrg, orgName } from './support';

// Drives the 2.10 markdown editor end to end on the version Content field: Edit ⇄ Preview toggle
// renders the markdown, the source round-trips unchanged, and an XSS payload is inert in the
// preview. Runs in a disposable org deleted on teardown.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('the version Content markdown editor previews, round-trips, and is XSS-safe [2.10]', async ({
  page,
  request,
}) => {
  const stamp = Date.now();
  const promptName = `e2e md ${stamp}`;
  orgId = await createOrg(request, orgName('md'));

  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgId);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();

  await page.getByTestId('toggle-add-version').click();

  // Author markdown, then toggle Preview — the structure renders.
  const source = '## Coaching\n\n- open strong\n- name patterns';
  await page.fill('#content', source);
  await page.getByTestId('md-preview-tab').click();
  const preview = page.getByTestId('md-preview');
  await expect(preview.locator('h2')).toHaveText('Coaching');
  await expect(preview.locator('li')).toHaveCount(2);

  // Back to Edit — the source is unchanged (preview is display-only).
  await page.getByTestId('md-edit-tab').click();
  await expect(page.locator('#content')).toHaveValue(source);

  // An XSS payload in the source is inert in the preview: no script runs, no dialog.
  let dialogFired = false;
  page.on('dialog', (d) => {
    dialogFired = true;
    void d.dismiss();
  });
  await page.fill('#content', '# Safe\n\n<img src="x" onerror="window.__mdxss = true">');
  await page.getByTestId('md-preview-tab').click();
  await expect(preview.locator('h1')).toHaveText('Safe');
  await expect(preview.locator('script')).toHaveCount(0);
  expect(
    await page.evaluate(() => (window as unknown as { __mdxss?: boolean }).__mdxss),
  ).toBeFalsy();
  expect(dialogFired).toBe(false);
});
