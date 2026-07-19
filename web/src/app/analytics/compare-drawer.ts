import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { PromptVersion } from '../prompt';
import { ScorerVariance, VersionComparison as VersionComparisonData } from '../analytics';
import { Drawer } from '../shared';
import { VersionDiff } from '../prompts/version-diff';
import { AnalyticsApiService } from './analytics-api.service';
import { VersionComparison } from './version-comparison';

type Tab = 'content' | 'scores' | 'rationale';

/**
 * The **unified Compare surface** (2.19 W7 / D1). One drawer replaces the two split-brain "Compare
 * versions" cards (workspace content-diff + analytics score-diff): pick From→To **once**, then switch
 * between **Content** (text diff), **Scores** (per-scorer deltas), and **Rationale** (the judge's
 * "why" on each side). Scores/Rationale need a dataset (that's where scored runs live); Content is
 * always available. Renders inside the shared right {@link Drawer}.
 */
@Component({
  selector: 'app-compare-drawer',
  imports: [Drawer, VersionDiff, VersionComparison],
  template: `
    <app-drawer [open]="open()" heading="Compare versions" (closed)="closed.emit()">
      <div class="compare">
        <form class="compare__pickers">
          <div class="sb-field">
            <label for="cmp-from">From</label>
            <select
              id="cmp-from"
              data-testid="compare-from"
              [value]="fromId() ?? ''"
              (change)="fromId.set($any($event.target).value)"
            >
              @for (v of versions(); track v.id) {
                <option [value]="v.id">{{ vLabel(v) }}</option>
              }
            </select>
          </div>
          <span class="compare__arrow">→</span>
          <div class="sb-field">
            <label for="cmp-to">To</label>
            <select
              id="cmp-to"
              data-testid="compare-to"
              [value]="toId() ?? ''"
              (change)="toId.set($any($event.target).value)"
            >
              @for (v of versions(); track v.id) {
                <option [value]="v.id">{{ vLabel(v) }}</option>
              }
            </select>
          </div>
        </form>

        <div class="compare__tabs" role="tablist">
          <button
            type="button"
            role="tab"
            class="compare__tab"
            [class.compare__tab--active]="tab() === 'content'"
            data-testid="compare-tab-content"
            (click)="tab.set('content')"
          >
            Content
          </button>
          <button
            type="button"
            role="tab"
            class="compare__tab"
            [class.compare__tab--active]="tab() === 'scores'"
            data-testid="compare-tab-scores"
            (click)="tab.set('scores')"
          >
            Scores
          </button>
          <button
            type="button"
            role="tab"
            class="compare__tab"
            [class.compare__tab--active]="tab() === 'rationale'"
            data-testid="compare-tab-rationale"
            (click)="tab.set('rationale')"
          >
            Rationale
          </button>
        </div>

        @if (sameVersion()) {
          <p class="compare__note" data-testid="compare-same">
            Pick two different versions to compare.
          </p>
        } @else {
          @switch (tab()) {
            @case ('content') {
              <app-version-diff [before]="fromContent()" [after]="toContent()" />
            }
            @default {
              @if (!datasetId()) {
                <p class="compare__note" data-testid="compare-need-dataset">
                  Scores and rationale compare scored runs — pick a dataset to see them.
                </p>
              } @else {
                @if (crossModel(); as cm) {
                  <p class="compare__warn" data-testid="cross-model-warning">
                    ⚠ These versions ran on different subject models ({{ cm.from }} vs {{ cm.to }}).
                    A score delta mixes the prompt change with a model change — hold the subject
                    model constant to compare the prompt cleanly.
                  </p>
                }
                @if (withinNoise(); as noise) {
                  <p class="compare__warn" data-testid="within-noise">
                    ⚠ This change (Δ {{ noise.delta.toFixed(3) }}) is within run-to-run noise (±{{
                      noise.spread.toFixed(3)
                    }}) — not a confident move. Run more repeats to confirm.
                  </p>
                }
                <app-version-comparison
                  [comparison]="comparison()"
                  [mode]="tab() === 'rationale' ? 'rationale' : 'scores'"
                />
              }
            }
          }
        }
      </div>
    </app-drawer>
  `,
  styles: [
    `
      .compare {
        display: flex;
        flex-direction: column;
        gap: var(--sb-space-lg);
      }
      .compare__pickers {
        display: flex;
        align-items: flex-end;
        gap: var(--sb-space-md);
      }
      .compare__pickers .sb-field {
        flex: 1;
      }
      .compare__arrow {
        padding-bottom: var(--sb-space-sm);
        color: var(--sb-text-muted);
      }
      .compare__tabs {
        display: flex;
        gap: var(--sb-space-xs);
        border-bottom: 1px solid var(--sb-border);
      }
      .compare__tab {
        border: none;
        background: transparent;
        padding: var(--sb-space-sm) var(--sb-space-md);
        cursor: pointer;
        color: var(--sb-text-muted);
        border-bottom: 2px solid transparent;
        font-size: var(--sb-type-body-size);
      }
      .compare__tab--active {
        color: var(--sb-text);
        border-bottom-color: var(--sb-primary);
        font-weight: 600;
      }
      .compare__note {
        color: var(--sb-text-muted);
        font-size: var(--sb-type-small-size);
      }
      .compare__warn {
        margin: 0;
        padding: var(--sb-space-sm) var(--sb-space-md);
        border: 1px solid var(--sb-warning);
        border-radius: var(--sb-radius-md);
        background: var(--sb-warning-surface);
        color: var(--sb-warning);
        font-size: var(--sb-type-small-size);
      }
    `,
  ],
})
export class CompareDrawer {
  private readonly api = inject(AnalyticsApiService);

  readonly open = input.required<boolean>();
  readonly promptId = input.required<string>();
  readonly versions = input.required<readonly PromptVersion[]>();
  /** Needed for the Scores/Rationale tabs (scored runs live in a dataset); Content works without. */
  readonly datasetId = input<string | null>(null);
  /** Per-scorer run-to-run spread — powers the "within noise" banner (R4/2.14); [] disables it. */
  readonly variance = input<readonly ScorerVariance[]>([]);
  readonly initialFromId = input<string | null>(null);
  readonly initialToId = input<string | null>(null);
  readonly closed = output<void>();

  protected readonly tab = signal<Tab>('content');
  protected readonly fromId = signal<string | null>(null);
  protected readonly toId = signal<string | null>(null);
  protected readonly comparison = signal<VersionComparisonData | null>(null);

  constructor() {
    // Seed From/To when the drawer opens: caller-provided ids, else the two most recent versions.
    effect(() => {
      if (!this.open()) return;
      const vs = this.versions();
      const n = vs.length;
      this.fromId.set(this.initialFromId() ?? (n >= 2 ? vs[n - 2].id : (vs[0]?.id ?? null)));
      this.toId.set(this.initialToId() ?? (n >= 1 ? vs[n - 1].id : null));
    });

    // Load the score/rationale comparison whenever the pair (or dataset) changes and both exist.
    effect(() => {
      const promptId = this.promptId();
      const datasetId = this.datasetId();
      const from = this.fromId();
      const to = this.toId();
      if (!this.open() || !datasetId || !from || !to || from === to) {
        this.comparison.set(null);
        return;
      }
      this.api.getComparison(promptId, datasetId, from, to).subscribe({
        next: (cmp) => this.comparison.set(cmp),
        error: () => this.comparison.set(null),
      });
    });
  }

  protected readonly sameVersion = computed(() => !!this.fromId() && this.fromId() === this.toId());

  private contentOf(id: string | null): string {
    return this.versions().find((v) => v.id === id)?.content ?? '';
  }
  protected readonly fromContent = computed(() => this.contentOf(this.fromId()));
  protected readonly toContent = computed(() => this.contentOf(this.toId()));

  // R5: the two versions ran on different subject models → a score delta confounds prompt vs model.
  protected readonly crossModel = computed<{ from: string; to: string } | null>(() => {
    const from = this.versions().find((v) => v.id === this.fromId());
    const to = this.versions().find((v) => v.id === this.toId());
    if (!from || !to || from.id === to.id) return null;
    return from.targetModel !== to.targetModel
      ? { from: from.targetModel, to: to.targetModel }
      : null;
  });

  // R4/2.14: the primary scorer's version-over-version delta sits inside the two versions' combined
  // run-to-run spread → not a confident move. Needs repeated runs (spread > 0) to say anything.
  protected readonly withinNoise = computed<{ spread: number; delta: number } | null>(() => {
    const cmp = this.comparison();
    const from = this.fromId();
    const to = this.toId();
    if (!cmp || !from || !to || cmp.scorers.length === 0) return null;
    const sc = cmp.scorers[0];
    if (sc.delta == null) return null;
    const sv = this.variance().find((v) => v.scorer.identity === sc.scorer.identity);
    const fromVar = sv?.versions.find((v) => v.promptVersionId === from);
    const toVar = sv?.versions.find((v) => v.promptVersionId === to);
    if (!fromVar || !toVar) return null;
    const spread = fromVar.aggregate.stdDev + toVar.aggregate.stdDev;
    if (spread <= 0 || Math.abs(sc.delta) > spread) return null;
    return { spread, delta: sc.delta };
  });

  protected vLabel(v: PromptVersion): string {
    return v.label ? `v${v.versionNumber} · ${v.label}` : `v${v.versionNumber}`;
  }
}
