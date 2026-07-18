import { test, expect } from '@playwright/test';
import { createOrg, deleteOrg, orgName } from './support';

// Exercises the score-analytics dashboard end to end against a live stack: create a prompt with
// two versions and, in its workspace, a dataset with fixtures + a scorer; run both versions, then
// open /analytics and confirm the trend chart, regression section, and version comparison render
// for that prompt × dataset. Runs make model calls, so this uses the stubbed eval-runner and is
// skipped by default (same gate as eval-run):
//   docker compose -f docker-compose.yml -f docker-compose.e2e.yml up -d --build
//   E2E_EVAL_RUNNER_STUB=1 npx playwright test e2e/analytics.spec.ts
// Runs in a disposable org, deleted on teardown.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('shows a score trend across versions and the regression section on the dashboard', async ({
  page,
  request,
}) => {
  test.skip(
    !process.env['E2E_EVAL_RUNNER_STUB'],
    'requires the stubbed eval-runner stack (docker-compose.e2e.yml)',
  );

  const stamp = Date.now();
  const promptName = `e2e analytics prompt ${stamp}`;
  const datasetName = `e2e analytics dataset ${stamp}`;
  orgId = await createOrg(request, orgName('analytics'));

  // 1. Prompt with two versions (in the disposable org).
  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgId);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  await page.getByTestId('prompts').getByRole('link', { name: promptName }).click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();
  await page.getByTestId('toggle-add-version').click();
  await page.fill('#content', 'good summarizer');
  await page.selectOption('#targetModel', 'claude-opus-4-8');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions').locator('tbody tr')).toHaveCount(1);
  await page.fill('#content', 'bad summarizer');
  await page.selectOption('#targetModel', 'claude-opus-4-8');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions').locator('tbody tr')).toHaveCount(2);

  // 2. A dataset under the prompt with two captured fixtures.
  await page.getByTestId('toggle-create-dataset').click();
  await page.fill('#datasetName', datasetName);
  await page.getByTestId('create-dataset').click();
  await page.getByTestId('datasets').getByRole('link', { name: datasetName }).click();
  await expect(page.getByRole('heading', { name: datasetName })).toBeVisible();
  const datasetUrl = page.url();
  await page.getByTestId('toggle-capture').click();
  await page.fill('#promptInput', 'summarize thread one');
  await page.getByTestId('capture').click();
  await expect(page.getByTestId('fixtures').locator('tr[data-origin="Captured"]')).toHaveCount(1);
  await page.fill('#promptInput', 'summarize thread two');
  await page.getByTestId('capture').click();
  await expect(page.getByTestId('fixtures').locator('tr[data-origin="Captured"]')).toHaveCount(2);

  // 3. A scorer.
  await page.getByTestId('toggle-add-scorer').click();
  await page.getByTestId('scorer-kind').selectOption('Regex');
  await page.getByTestId('scorer-config').fill('.+');
  await page.getByTestId('add-scorer').click();
  await expect(page.getByTestId('scorers').locator('tbody tr')).toHaveCount(1);

  // 4. Run each version (prompt is fixed to the dataset's owner — B3; pick a version).
  await page.getByTestId('version-select').selectOption({ index: 0 });
  await page.getByTestId('run').click();
  await expect(page).toHaveURL(/\/eval-runs\//);

  await page.goto(datasetUrl);
  await page.getByTestId('version-select').selectOption({ index: 1 });
  await page.getByTestId('run').click();
  await expect(page).toHaveURL(/\/eval-runs\//);

  // 5. Dashboard: select the prompt + dataset and see the trend + regression section.
  await page.goto('/analytics');
  await page.getByTestId('prompt-select').selectOption({ label: promptName });
  await page.getByTestId('dataset-select').selectOption({ label: datasetName });

  await expect(page.getByTestId('trend-chart')).toBeVisible();
  await expect(page.locator('[data-testid="trend-chart"] .line-chart')).toBeVisible();
  await expect(
    page.getByTestId('regressions').or(page.getByTestId('no-regressions')),
  ).toBeVisible();

  // 6. Version comparison defaults to the two latest versions and shows per-fixture deltas.
  await expect(page.getByTestId('from-version')).toBeVisible();
  await expect(page.getByTestId('to-version')).toBeVisible();
  await expect(page.getByTestId('scorer-comparison').first()).toBeVisible();
  await expect(page.getByTestId('fixture-delta-row').first()).toBeVisible();
});
