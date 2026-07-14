import { defineConfig, devices } from '@playwright/test';
import path from 'node:path';

const authFile = path.join(__dirname, 'e2e', '.auth', 'user.json');

/**
 * E2E runs against an already-running stack (the composed stack in CI, or a local
 * `docker compose up`). Point it elsewhere with E2E_BASE_URL.
 *
 * Auth (4.1): every app route is guarded, so a `setup` project registers one user and saves its
 * session to `storageState`; the `chromium` project depends on it and loads that state, starting
 * both the page and the `request` fixture authenticated. Specs that must be unauthenticated
 * (e.g. the auth happy-path) override `storageState` locally.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4240',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'setup', testMatch: /.*\.setup\.ts/ },
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], storageState: authFile },
      dependencies: ['setup'],
    },
  ],
});
