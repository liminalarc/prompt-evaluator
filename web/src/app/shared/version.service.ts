import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/** The flat GET /api/version payload (spec 3.3, Stormboard pattern). */
export interface VersionInfo {
  version: string;
  commit: string;
  buildTime: string;
  /** ASPNETCORE_ENVIRONMENT — reported but deliberately NOT the dev/prod discriminator. */
  environment: string;
  /** DEPLOY_CHANNEL — the reliable dev/prod signal: "local" | "dev" | "staging" | "prod" | … */
  channel: string;
}

/**
 * Loads the running build's version once at startup and exposes it (plus the derived footer-chip
 * and env-badge labels) as signals. Display is non-essential: a failed fetch leaves everything null
 * so the indicators simply don't render — never an error surfaced to the user.
 */
@Injectable({ providedIn: 'root' })
export class VersionService {
  private readonly http = inject(HttpClient);

  private readonly _info = signal<VersionInfo | null>(null);
  readonly info: Signal<VersionInfo | null> = this._info.asReadonly();

  /**
   * Footer build-chip label, channel-keyed. A dev build shows channel+sha rather than a semver so
   * it never looks "behind" a freshly-tagged prod.
   */
  readonly buildLabel = computed<string | null>(() => {
    const v = this._info();
    if (!v) return null;
    const short = this.shortCommit(v.commit);
    switch (v.channel) {
      case 'prod':
        return short ? `v${v.version} · ${short}` : `v${v.version}`;
      case 'local':
        return short ? `local · ${short}` : 'local';
      default:
        return short ? `${v.channel} · ${short}` : v.channel;
    }
  });

  /** Full detail for the chip's tooltip. */
  readonly buildTooltip = computed<string | null>(() => {
    const v = this._info();
    if (!v) return null;
    return `${v.channel} · v${v.version} · ${v.commit} · built ${v.buildTime}`;
  });

  /**
   * Topbar environment badge: the uppercased channel, but nothing in prod (a prod badge is noise).
   * Channel wins over `environment` — the host may report "Production" everywhere.
   */
  readonly envBadge = computed<string | null>(() => {
    const v = this._info();
    if (!v) return null;
    return v.channel === 'prod' ? null : v.channel.toUpperCase();
  });

  async load(): Promise<void> {
    try {
      this._info.set(await firstValueFrom(this.http.get<VersionInfo>('/api/version')));
    } catch {
      /* non-essential — leave null and render nothing */
    }
  }

  private shortCommit(commit: string): string | null {
    // "dev" / "local-dev" are the no-real-commit defaults; anything else is a real sha.
    if (!commit || commit === 'dev' || commit === 'local-dev') return null;
    return commit.slice(0, 7);
  }
}
