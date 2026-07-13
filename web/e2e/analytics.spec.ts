import { test, expect } from '@playwright/test';

// Exercises the score-analytics dashboard end to end against a live stack: create a prompt with
// two versions, a dataset with fixtures and a scorer, run both versions, then open /analytics and
// confirm the trend chart and regression section render for that prompt × dataset. Runs make model
// calls, so this uses the stubbed eval-runner and is skipped by default (same gate as eval-run):
//   docker compose -f docker-compose.yml -f docker-compose.e2e.yml up -d --build
//   E2E_EVAL_RUNNER_STUB=1 npx playwright test e2e/analytics.spec.ts
test('shows a score trend across versions and the regression section on the dashboard', async ({
  page,
}) => {
  test.skip(
    !process.env['E2E_EVAL_RUNNER_STUB'],
    'requires the stubbed eval-runner stack (docker-compose.e2e.yml)',
  );

  const stamp = Date.now();
  const promptName = `e2e analytics prompt ${stamp}`;
  const datasetName = `e2e analytics dataset ${stamp}`;

  // 1. Prompt with two versions.
  await page.goto('/prompts');
  await page.fill('#name', promptName);
  await page.getByTestId('create').click();
  await page.getByTestId('prompts').getByText(promptName).click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();
  await page.fill('#content', 'good summarizer');
  await page.fill('#targetModel', 'claude-opus-4-8');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions').locator('tbody tr')).toHaveCount(1);
  await page.fill('#content', 'bad summarizer');
  await page.fill('#targetModel', 'claude-opus-4-8');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions').locator('tbody tr')).toHaveCount(2);

  // 2. Dataset with two captured fixtures.
  await page.goto('/datasets');
  await page.fill('#name', datasetName);
  await page.getByTestId('create').click();
  await page.getByTestId('datasets').getByText(datasetName).click();
  await expect(page.getByRole('heading', { name: datasetName })).toBeVisible();
  const datasetUrl = page.url();
  await page.fill('#promptInput', 'summarize thread one');
  await page.getByTestId('capture').click();
  // Wait for the first capture to land (it clears the input) before capturing the second.
  await expect(page.getByTestId('fixtures').locator('tr[data-origin="Captured"]')).toHaveCount(1);
  await page.fill('#promptInput', 'summarize thread two');
  await page.getByTestId('capture').click();
  await expect(page.getByTestId('fixtures').locator('tr[data-origin="Captured"]')).toHaveCount(2);

  // 3. A scorer.
  await page.getByTestId('scorer-kind').selectOption('Regex');
  await page.getByTestId('scorer-config').fill('.+');
  await page.getByTestId('add-scorer').click();
  await expect(page.getByTestId('scorers').locator('tbody tr')).toHaveCount(1);

  // 4. Run each version.
  await page.getByTestId('prompt-select').selectOption({ label: promptName });
  await page.getByTestId('version-select').selectOption({ index: 0 });
  await page.getByTestId('run').click();
  await expect(page).toHaveURL(/\/eval-runs\//);

  await page.goto(datasetUrl);
  await page.getByTestId('prompt-select').selectOption({ label: promptName });
  await page.getByTestId('version-select').selectOption({ index: 1 });
  await page.getByTestId('run').click();
  await expect(page).toHaveURL(/\/eval-runs\//);

  // 5. Dashboard: select the prompt + dataset and see the trend + regression section.
  await page.goto('/analytics');
  await page.getByTestId('prompt-select').selectOption({ label: promptName });
  await page.getByTestId('dataset-select').selectOption({ label: datasetName });

  await expect(page.getByTestId('trend-chart')).toBeVisible();
  // Two versions → the line chart plots two points; ngx-charts renders circle markers.
  await expect(page.locator('[data-testid="trend-chart"] .line-chart')).toBeVisible();
  // The regressions section resolves to either a flagged row or the clean state.
  await expect(
    page.getByTestId('regressions').or(page.getByTestId('no-regressions')),
  ).toBeVisible();

  // 6. Version comparison defaults to the two latest versions and shows per-fixture deltas.
  await expect(page.getByTestId('from-version')).toBeVisible();
  await expect(page.getByTestId('to-version')).toBeVisible();
  await expect(page.getByTestId('scorer-comparison').first()).toBeVisible();
  await expect(page.getByTestId('fixture-delta-row').first()).toBeVisible();
});
