import { test, expect } from '@playwright/test';

// Exercises the in-browser eval-run flow end to end: configure a scorer, run a prompt version over
// a dataset, and view the per-fixture scores. Prompt execution + judging normally make live Claude
// calls, so this runs only against the stubbed eval-runner (deterministic, no key):
//   docker compose -f docker-compose.yml -f docker-compose.e2e.yml up -d --build
//   E2E_EVAL_RUNNER_STUB=1 npx playwright test e2e/eval-run.spec.ts
// It is skipped by default so the normal e2e run never hits the model.
test('runs a prompt version over a dataset and shows per-fixture scores', async ({ page }) => {
  test.skip(
    !process.env['E2E_EVAL_RUNNER_STUB'],
    'requires the stubbed eval-runner stack (docker-compose.e2e.yml)',
  );

  const stamp = Date.now();
  const promptName = `e2e run prompt ${stamp}`;
  const datasetName = `e2e run dataset ${stamp}`;

  // 1. Create a prompt with a version.
  await page.goto('/prompts');
  await page.fill('#name', promptName);
  await page.getByTestId('create').click();
  await page.getByTestId('prompts').getByText(promptName).click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();
  await page.fill('#content', 'You summarize text.');
  await page.fill('#targetModel', 'claude-opus-4-8');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions').locator('tbody tr')).toHaveCount(1);

  // 2. Create a dataset with a captured fixture.
  await page.goto('/datasets');
  await page.fill('#name', datasetName);
  await page.getByTestId('create').click();
  await page.getByTestId('datasets').getByText(datasetName).click();
  await expect(page.getByRole('heading', { name: datasetName })).toBeVisible();
  await page.fill('#promptInput', 'summarize this thread');
  await page.getByTestId('capture').click();
  await expect(page.getByTestId('fixtures').locator('tr[data-origin="Captured"]')).toHaveCount(1);

  // 3. Configure a deterministic scorer.
  await page.getByTestId('scorer-kind').selectOption('Regex');
  await page.getByTestId('scorer-config').fill('.+');
  await page.getByTestId('add-scorer').click();
  await expect(page.getByTestId('scorers').locator('tbody tr')).toHaveCount(1);

  // 4. Run the evaluation: pick the prompt + version, then run.
  await page.getByTestId('prompt-select').selectOption({ label: promptName });
  await page.getByTestId('version-select').selectOption({ index: 0 });
  await page.getByTestId('run').click();

  // 5. Land on the run-results view with per-fixture scores.
  await expect(page).toHaveURL(/\/eval-runs\//);
  const fixtureRun = page.getByTestId('fixture-run');
  await expect(fixtureRun).toHaveCount(1);
  // The stub executes as "[executed:<model>] <input>".
  await expect(fixtureRun).toContainText('summarize this thread');
  await expect(page.getByTestId('scores').locator('tr[data-scorer="Regex"]')).toHaveCount(1);
});
