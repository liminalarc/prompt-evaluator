import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Organization } from '../organization';
import { OrganizationsApiService } from '../organizations/organizations-api.service';

const STORAGE_KEY = 'litmus.currentOrgId';

/**
 * The persistent global organization context (2.4). Organization is the top-level boundary (1.9);
 * before this it existed only inside the Prompts page, so every other surface ignored it. This
 * root store makes the selection global — the left org rail (W39) writes it, and prompts / datasets /
 * analytics / dashboard all read from it.
 *
 * The selection is remembered in **localStorage** and mirrored to a **`?org=` query param** so a
 * URL is shareable/deep-linkable. Initial resolution: `?org=` → localStorage → first org.
 */
@Injectable({ providedIn: 'root' })
export class OrgContextStore {
  private readonly api = inject(OrganizationsApiService);
  private readonly router = inject(Router);

  private readonly _orgs = signal<Organization[]>([]);
  private readonly _currentId = signal<string | null>(null);

  readonly organizations: Signal<Organization[]> = this._orgs.asReadonly();
  readonly currentOrgId: Signal<string | null> = this._currentId.asReadonly();
  readonly currentOrg = computed(
    () => this._orgs().find((o) => o.id === this._currentId()) ?? null,
  );

  private loaded = false;

  /** Load the org list once and resolve the initial selection: `?org=` → localStorage → first. */
  load(): void {
    if (this.loaded) return;
    this.loaded = true;
    this.api.listOrganizations().subscribe({
      next: (orgs) => {
        this._orgs.set(orgs);
        const known = (id: string | null): id is string => !!id && orgs.some((o) => o.id === id);
        const fromUrl = this.urlOrg();
        const fromStore = this.storedOrg();
        const resolved = known(fromUrl)
          ? fromUrl
          : known(fromStore)
            ? fromStore
            : (orgs[0]?.id ?? null);
        if (resolved) this.select(resolved);
      },
    });
  }

  /** Switch the active org — updates the signal, localStorage, and the `?org=` query param. */
  select(id: string): void {
    if (id === this._currentId()) return;
    this._currentId.set(id);
    this.persist(id);
    this.router.navigate([], {
      queryParams: { org: id },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  /** Append a freshly created org and make it current (the create-org flow on the Prompts page). */
  add(org: Organization): void {
    this._orgs.update((list) => [...list, org]);
    this.select(org.id);
  }

  /**
   * Drop a deleted org from the context (1.10). If it was the current one, repoint to the first
   * remaining org, or clear the selection entirely when none are left (clears the `?org=` param).
   */
  remove(id: string): void {
    const wasCurrent = this._currentId() === id;
    this._orgs.update((list) => list.filter((o) => o.id !== id));
    if (!wasCurrent) return;

    const next = this._orgs()[0]?.id ?? null;
    this._currentId.set(null); // clear first so select() re-navigates even to a new id
    if (next) {
      this.select(next);
    } else {
      try {
        localStorage.removeItem(STORAGE_KEY);
      } catch {
        /* localStorage unavailable — in-memory clear still applies */
      }
      this.router.navigate([], {
        queryParams: { org: null },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
    }
  }

  /** Clear the context so the next `load()` re-fetches — used on sign-out (4.1). */
  reset(): void {
    this.loaded = false;
    this._orgs.set([]);
    this._currentId.set(null);
  }

  private urlOrg(): string | null {
    return this.router.parseUrl(this.router.url).queryParams['org'] ?? null;
  }

  private storedOrg(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }

  private persist(id: string): void {
    try {
      localStorage.setItem(STORAGE_KEY, id);
    } catch {
      /* localStorage unavailable (private mode) — in-memory selection still works */
    }
  }
}
