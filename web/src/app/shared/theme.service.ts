import { Injectable, Signal, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'litmus.theme';

/**
 * Owns the light/dark theme (2.5). The selection persists to **localStorage** and is applied
 * by stamping `data-theme` on `<html>` — the brand tokens read that attribute to swap palettes.
 * Constructed at boot (root-provided) so the persisted theme is applied before first paint,
 * replacing the hard-pinned `data-theme="light"` that used to live in `index.html`.
 *
 * `localStorage` / `document` access is guarded so an SSR/prerender pass (no browser globals)
 * degrades to the default instead of throwing — mirrors {@link OrgContextStore}'s try/catch.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _theme = signal<Theme>('light');
  readonly theme: Signal<Theme> = this._theme.asReadonly();

  constructor() {
    this.apply(this.stored() ?? 'light');
  }

  /** Flip between light and dark, persisting + applying the new choice. */
  toggle(): void {
    this.apply(this._theme() === 'dark' ? 'light' : 'dark');
  }

  private apply(theme: Theme): void {
    this._theme.set(theme);
    this.persist(theme);
    try {
      document.documentElement.setAttribute('data-theme', theme);
    } catch {
      /* no document (SSR/prerender) — the in-memory signal still tracks the choice */
    }
  }

  private stored(): Theme | null {
    try {
      const value = localStorage.getItem(STORAGE_KEY);
      return value === 'light' || value === 'dark' ? value : null;
    } catch {
      return null;
    }
  }

  private persist(theme: Theme): void {
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      /* localStorage unavailable (private mode / SSR) — selection stays in memory */
    }
  }
}
