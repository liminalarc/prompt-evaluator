import { Component, Input, computed, signal } from '@angular/core';
import {
  FixtureDelta,
  ScorerComparison,
  VersionComparison as VersionComparisonData,
  scorerLabel,
} from '../analytics';

/** Which facet of the comparison to render. `both` = score table with inline "Why" (default,
 *  standalone use); `scores` / `rationale` split them into the Compare drawer's tabs (W7). */
export type ComparisonMode = 'both' | 'scores' | 'rationale';

/**
 * Presentational version-vs-version comparison: per scorer, the aggregate mean on each side and its
 * delta, plus a per-fixture delta breakdown. A positive delta is an improvement, negative a
 * regression; deltas carry a ▲/▼ glyph so direction is never color-alone. Dumb — data in.
 */
@Component({
  selector: 'app-version-comparison',
  template: `
    @if (data(); as cmp) {
      <p class="caption">
        Comparing <strong>v{{ cmp.fromVersionNumber }}</strong> →
        <strong>v{{ cmp.toVersionNumber }}</strong>
      </p>
      @if (cmp.scorers.length === 0) {
        <p class="empty" data-testid="comparison-empty">
          No overlapping scored runs for these two versions.
        </p>
      } @else {
        @for (sc of cmp.scorers; track sc.scorer.identity) {
          <div class="scorer-block" data-testid="scorer-comparison">
            <h3 class="scorer-title">{{ label(sc) }}</h3>

            @if (showScores()) {
              <p class="aggregate">
                <span>{{ fmt(sc.fromMean) }}</span>
                <span class="arrow">→</span>
                <span>{{ fmt(sc.toMean) }}</span>
                <span class="delta" [class]="deltaClass(sc.delta)" data-testid="aggregate-delta">
                  {{ glyph(sc.delta) }} {{ fmt(sc.delta) }}
                </span>
              </p>
              <table class="sb-table" data-testid="fixture-deltas">
                <thead>
                  <tr>
                    <th>Test case</th>
                    <th>v{{ cmp.fromVersionNumber }}</th>
                    <th>v{{ cmp.toVersionNumber }}</th>
                    <th>Δ</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (f of sc.fixtures; track f.fixtureId) {
                    <tr data-testid="fixture-delta-row">
                      <td [class.mono]="!f.fixtureLabel">
                        {{ f.fixtureLabel ?? shortId(f.fixtureId) }}
                      </td>
                      <td>{{ fmt(f.fromValue) }}</td>
                      <td>{{ fmt(f.toValue) }}</td>
                      <td class="delta" [class]="deltaClass(f.delta)">
                        {{ glyph(f.delta) }} {{ fmt(f.delta) }}
                      </td>
                      <td>
                        @if (showInlineRationale() && (f.fromRationale || f.toRationale)) {
                          <button
                            type="button"
                            class="sb-btn sb-btn--ghost sb-btn--sm"
                            data-testid="rationale-toggle"
                            (click)="toggle(f.fixtureId)"
                          >
                            {{ expandedFixtureId() === f.fixtureId ? 'Hide why' : 'Why' }}
                          </button>
                        }
                      </td>
                    </tr>
                    @if (showInlineRationale() && expandedFixtureId() === f.fixtureId) {
                      <tr class="rationale-row" data-testid="rationale-diff">
                        <td colspan="5">
                          <div class="rationale-diff">
                            <div class="rationale-side">
                              <h4>v{{ cmp.fromVersionNumber }} — why</h4>
                              <p>{{ f.fromRationale ?? '— (not scored on this side)' }}</p>
                            </div>
                            <div class="rationale-side">
                              <h4>v{{ cmp.toVersionNumber }} — why</h4>
                              <p>{{ f.toRationale ?? '— (not scored on this side)' }}</p>
                            </div>
                          </div>
                        </td>
                      </tr>
                    }
                  }
                </tbody>
              </table>
            }

            @if (currentMode() === 'rationale') {
              @for (f of rationaleFixtures(sc); track f.fixtureId) {
                <div class="rationale-diff" data-testid="rationale-diff">
                  <div class="rationale-side">
                    <h4>
                      {{ f.fixtureLabel ?? shortId(f.fixtureId) }} — v{{ cmp.fromVersionNumber }}
                    </h4>
                    <p>{{ f.fromRationale ?? '— (not scored on this side)' }}</p>
                  </div>
                  <div class="rationale-side">
                    <h4>
                      {{ f.fixtureLabel ?? shortId(f.fixtureId) }} — v{{ cmp.toVersionNumber }}
                    </h4>
                    <p>{{ f.toRationale ?? '— (not scored on this side)' }}</p>
                  </div>
                </div>
              } @empty {
                <p class="empty">No judge rationale captured for these versions.</p>
              }
            }
          </div>
        }
      }
    }
  `,
  styleUrl: './version-comparison.css',
})
export class VersionComparison {
  private readonly _data = signal<VersionComparisonData | null>(null);
  private readonly _mode = signal<ComparisonMode>('both');

  @Input() set comparison(value: VersionComparisonData | null) {
    this._data.set(value);
  }

  @Input() set mode(value: ComparisonMode) {
    this._mode.set(value);
  }

  protected readonly data = computed(() => this._data());
  protected readonly currentMode = computed(() => this._mode());
  // 'both' and 'scores' show the score table; only 'both' carries the inline Why (rationale is its
  // own tab under 'rationale').
  protected readonly showScores = computed(() => this._mode() !== 'rationale');
  protected readonly showInlineRationale = computed(() => this._mode() === 'both');

  /** Fixtures carrying judge rationale on either side — the rationale-only tab's rows. */
  protected rationaleFixtures(sc: ScorerComparison): FixtureDelta[] {
    return sc.fixtures.filter((f) => f.fromRationale || f.toRationale);
  }

  // Rationale-diff (2.14): a fixture row expands to show the judge's reasoning on each side, so a
  // flat-scoring but real quality change is legible ("removed the prediction") — not just its delta.
  protected readonly expandedFixtureId = signal<string | null>(null);
  protected toggle(fixtureId: string): void {
    this.expandedFixtureId.update((cur) => (cur === fixtureId ? null : fixtureId));
  }

  protected label(sc: ScorerComparison): string {
    return scorerLabel(sc.scorer);
  }

  protected shortId(id: string): string {
    return id.slice(0, 8);
  }

  protected fmt(value: number | null): string {
    return value == null ? '—' : value.toFixed(3);
  }

  protected glyph(delta: number | null): string {
    if (delta == null || delta === 0) return '';
    return delta > 0 ? '▲' : '▼';
  }

  protected deltaClass(delta: number | null): string {
    if (delta == null || delta === 0) return 'delta--flat';
    return delta > 0 ? 'delta--up' : 'delta--down';
  }
}
