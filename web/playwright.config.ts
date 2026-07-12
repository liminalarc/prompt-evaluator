import { defineConfig, devices } from '@playwright/test';

/**
 * E2E runs against an already-running stack (the composed stack in CI, or a local
 * `docker compose up`). Point it elsewhere with E2E_BASE_URL.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4240',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
