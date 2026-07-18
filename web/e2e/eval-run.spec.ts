import { test, expect } from '@playwright/test';
import { createOrg, deleteOrg, orgName } from './support';

// Exercises the in-browser eval-run flow end to end: create a prompt (with a version) and, in its
// workspace, a dataset; then on the dataset page configure a scorer, run the prompt version over
// the dataset, and view per-fixture scores. Prompt execution + judging normally make live Claude
// calls, so this runs only against the stubbed eval-runner (deterministic, no key):
//   docker compose -f docker-compose.yml -f docker-compose.e2e.yml up -d --build
//   E2E_EVAL_RUNNER_STUB=1 npx playwright test e2e/eval-run.spec.ts
// Skipped by default so the normal e2e run never hits the model. Runs in a disposable org.
let orgId = '';

test.afterEach(async ({ request }) => {
  await deleteOrg(request, orgId);
  orgId = '';
});

test('runs a prompt version over a dataset and shows per-fixture scores', async ({
  page,
  request,
}) => {
  test.skip(
    !process.env['E2E_EVAL_RUNNER_STUB'],
    'requires the stubbed eval-runner stack (docker-compose.e2e.yml)',
  );

  const stamp = Date.now();
  const promptName = `e2e run prompt ${stamp}`;
  const datasetName = `e2e run dataset ${stamp}`;
  orgId = await createOrg(request, orgName('eval-run'));

  // 1. Create a prompt (in the disposable org) with a version.
  await page.goto('/prompts');
  await page.getByTestId('org-select').selectOption(orgId);
  await page.getByTestId('toggle-new-prompt').click();
  await page.fill('#name', promptName);
  await page.getByTestId('create-prompt').click();
  await page.getByTestId('prompts').getByRole('link', { name: promptName }).click();
  await expect(page.getByRole('heading', { name: promptName })).toBeVisible();
  await page.getByTestId('toggle-add-version').click();
  await page.fill('#content', 'You summarize text.');
  await page.selectOption('#targetModel', 'claude-opus-4-8');
  await page.getByTestId('add-version').click();
  await expect(page.getByTestId('versions').locator('tbody tr')).toHaveCount(1);

  // 2. Create a dataset under the prompt (in its workspace), then open it.
  await page.getByTestId('toggle-create-dataset').click();
  await page.fill('#datasetName', datasetName);
  await page.getByTestId('create-dataset').click();
  await page.getByTestId('datasets').getByRole('link', { name: datasetName }).click();
  await expect(page.getByRole('heading', { name: datasetName })).toBeVisible();

  // 3. Capture a fixture.
  await page.getByTestId('toggle-capture').click();
  await page.fill('#promptInput', 'summarize this thread');
  await page.getByTestId('capture').click();
  await expect(page.getByTestId('fixtures').locator('tr[data-origin="Captured"]')).toHaveCount(1);

  // 4. Configure a deterministic scorer.
  await page.getByTestId('toggle-add-scorer').click();
  await page.getByTestId('scorer-kind').selectOption('Regex');
  await page.getByTestId('scorer-config').fill('.+');
  await page.getByTestId('add-scorer').click();
  await expect(page.getByTestId('scorers').locator('tbody tr')).toHaveCount(1);

  // 5. Run the evaluation: the prompt is fixed to the dataset's owner (B3) — pick a version, run.
  await expect(page.getByTestId('run-prompt')).toContainText(promptName);
  await page.getByTestId('version-select').selectOption({ index: 0 });
  await page.getByTestId('run').click();

  // 6. Land on the run-results view; expand the fixture summary row for per-fixture scores (U10).
  await expect(page).toHaveURL(/\/eval-runs\//);
  const fixtureRun = page.getByTestId('fixture-run');
  await expect(fixtureRun).toHaveCount(1);
  await expect(fixtureRun).toContainText('summarize this thread');
  await page.getByTestId('fixture-run-summary').click();
  await expect(page.getByTestId('scores').locator('tr[data-scorer="Regex"]')).toHaveCount(1);
});
