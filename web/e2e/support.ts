import { APIRequestContext } from '@playwright/test';
import path from 'node:path';

// Shared e2e helpers: each spec does its work inside a disposable organization and deletes it on
// teardown (deleting an org cascades to its folders, prompts, and datasets), so e2e runs never
// leave data behind in the local app.

// Where the shared authenticated session is persisted. `auth.setup.ts` writes it; the `chromium`
// project loads it as `storageState` (see playwright.config.ts), so specs start signed in (4.1).
export const authFile = path.join(__dirname, '.auth', 'user.json');

/** A unique org name for a spec run. */
export function orgName(prefix: string): string {
  return `e2e ${prefix} ${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
}

/** Creates an organization via the API and returns its id. */
export async function createOrg(request: APIRequestContext, name: string): Promise<string> {
  const res = await request.post('/api/organizations', { data: { name } });
  return (await res.json()).id;
}

/** Deletes an organization (and everything under it). Safe to call with an empty id. */
export async function deleteOrg(request: APIRequestContext, id: string): Promise<void> {
  if (id) {
    await request.delete(`/api/organizations/${id}`);
  }
}
