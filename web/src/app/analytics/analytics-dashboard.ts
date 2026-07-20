import { Component, computed, effect, inject, signal } from '@angular/core';
import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import {
  CompositeTrendPoint,
  RegressionFlag,
  ScorerRef,
  ScorerVariance,
  TrendSeries,
  VarianceStat,
  isStochasticScorer,
  scorerLabel,
} from '../analytics';
import { AnalyticsApiService } from './analytics-api.service';
import { EvalRunsApiService } from '../eval-runs/eval-runs-api.service';
import { PromptsApiService } from '../prompts/prompts-api.service';
import { DatasetsApiService } from '../datasets/datasets-api.service';
import { OrgContextStore } from '../shared/org-context.store';
import { PromptSummary, PromptVersion } from '../prompt';
import { DatasetSummary } from '../dataset';
import { TrendChart } from './trend-chart';
import { VersionChangeTable } from './version-change-table';
import { CompareDrawer } from './compare-drawer';
import { BadgeVariant, Card, EmptyState, ErrorState, PageHeader, StatusBadge } from '../shared';

/**
 * Score-tracking dashboard: pick a prompt + dataset, then see the score trend across versions
 * (one line per scorer) and the regression flags between consecutive versions.
 */
@Component({
  selector: 'app-analytics-dashboard',
  imports: [
    FormsModule,
    DecimalPipe,
    NgTemplateOutlet,
    TrendChart,
    VersionChangeTable,
    CompareDrawer,
    Card,
    EmptyState,
    ErrorState,
    PageHeader,
    StatusBadge,
  ],
  template: `
    <section class="panel panel--wide">
      <app-page-header
        heading="Score Analytics"
        subtitle="Trends and regressions per Prompt × Version × Dataset × Scorer."
      />

      <app-card>
        <form class="selectors">
          <div class="sb-field">
            <label for="prompt">Prompt</label>
            <select
              id="prompt"
              name="prompt"
              data-testid="prompt-select"
              [ngModel]="promptId()"
              (ngModelChange)="selectPrompt($event)"
            >
              <option [ngValue]="null">Select a prompt…</option>
              @for (p of prompts(); track p.id) {
                <option [ngValue]="p.id">{{ p.name }}</option>
              }
            </select>
          </div>
          <div class="sb-field">
            <label for="dataset">Dataset</label>
            <select
              id="dataset"
              name="dataset"
              data-testid="dataset-select"
              [ngModel]="datasetId()"
              (ngModelChange)="datasetId.set($event)"
            >
              <option [ngValue]="null">Select a dataset…</option>
              @for (d of datasetsForPrompt(); track d.id) {
                <option [ngValue]="d.id">{{ d.name }}</option>
              }
            </select>
          </div>
          <div class="sb-field">
            <label for="threshold">Regression threshold</label>
            <input
              id="threshold"
              name="threshold"
              type="number"
              min="0"
              max="1"
              step="0.01"
              data-testid="threshold"
              [ngModel]="threshold()"
              (ngModelChange)="threshold.set($event)"
            />
          </div>
        </form>
      </app-card>

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      @if (promptId() && datasetId()) {
        <app-card heading="Score trend">
          <app-trend-chart [series]="visibleTrends()" [composite]="composite()" />
          @if (unrunVersions().length > 0) {
            <p class="subtitle" data-testid="unrun-versions">
              No runs yet (not on the trend):
              <strong>{{ unrunVersionsLabel() }}</strong> — run them to compare.
            </p>
          }
        </app-card>

        <app-card heading="Version-over-version change">
          <p class="subtitle">
            Each version's per-scorer mean and the <strong>weighted composite</strong>, with the
            change vs the prior version.
          </p>
          <app-version-change-table [series]="visibleTrends()" [composite]="composite()" />
        </app-card>

        @if (stochasticVariance().length > 0 || hiddenVariance().length > 0) {
          <app-card heading="Score stability">
            <p class="subtitle">
              Mean ± spread over each version's <strong>repeated runs</strong>. Only
              <strong>LLM-judge</strong> scorers vary run-to-run; deterministic scorers are always
              ±0.000, so they're hidden below. Run a version more than once to build the
              distribution.
            </p>

            @for (sv of stochasticVariance(); track sv.scorer.identity) {
              <div class="variance-block" data-testid="variance-scorer">
                <h3 class="scorer-title">{{ scorerBlockLabel(sv.scorer) }}</h3>
                <ng-container
                  [ngTemplateOutlet]="varianceTable"
                  [ngTemplateOutletContext]="{ $implicit: sv }"
                />
              </div>
            } @empty {
              @if (hiddenVariance().length > 0) {
                <app-empty-state
                  message="No stochastic scorers to track — stability applies to LLM-judge scorers."
                  data-testid="no-stochastic-variance"
                />
              }
            }

            @if (hiddenVariance().length > 0) {
              <button
                type="button"
                class="sb-btn sb-btn--sm sb-btn--ghost"
                (click)="showHiddenVariance.set(!showHiddenVariance())"
                data-testid="toggle-hidden-variance"
              >
                {{ showHiddenVariance() ? 'Hide' : 'Show' }}
                {{ hiddenVariance().length }} deterministic / stale scorer{{
                  hiddenVariance().length === 1 ? '' : 's'
                }}
              </button>
              @if (showHiddenVariance()) {
                @for (sv of hiddenVariance(); track sv.scorer.identity) {
                  <div
                    class="variance-block variance-block--muted"
                    data-testid="variance-scorer-hidden"
                  >
                    <h3 class="scorer-title">
                      {{ scorerBlockLabel(sv.scorer) }}
                      @if (isStale(sv.scorer)) {
                        <app-status-badge variant="warn" label="stale config" />
                      } @else {
                        <span class="scorer-tag">deterministic</span>
                      }
                    </h3>
                    <ng-container
                      [ngTemplateOutlet]="varianceTable"
                      [ngTemplateOutletContext]="{ $implicit: sv }"
                    />
                  </div>
                }
              }
            }
          </app-card>

          <ng-template #varianceTable let-sv>
            <table class="sb-table" data-testid="variance">
              <thead>
                <tr>
                  <th>Version</th>
                  <th>Runs</th>
                  <th>Mean ± spread</th>
                  <th>Range</th>
                </tr>
              </thead>
              <tbody>
                @for (v of sv.versions; track v.promptVersionId) {
                  <tr data-testid="variance-row">
                    <td>
                      v{{ v.versionNumber }}{{ v.versionLabel ? ' · ' + v.versionLabel : '' }}
                    </td>
                    <td>{{ v.runCount }}</td>
                    <td>{{ fmtStat(v.aggregate) }}</td>
                    <td>{{ v.aggregate.min.toFixed(3) }}–{{ v.aggregate.max.toFixed(3) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </ng-template>
        }

        <div class="card-grid">
          <app-card heading="Regressions">
            @if (regressions(); as flags) {
              @if (flags.length === 0) {
                <app-empty-state
                  message="No regressions beyond the threshold."
                  data-testid="no-regressions"
                />
              } @else {
                @if (confirmed().length > 0) {
                  <table class="sb-table" data-testid="regressions">
                    <thead>
                      <tr>
                        <th>Scorer</th>
                        <th>From → To</th>
                        <th>Prior</th>
                        <th>Current</th>
                        <th>Δ</th>
                        <th>p-value</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (f of confirmed(); track f.scorer.identity + f.toVersionId) {
                        <tr data-testid="regression-row">
                          <td>{{ label(f) }}</td>
                          <td>v{{ f.fromVersionNumber }} → v{{ f.toVersionNumber }}</td>
                          <td>{{ f.priorMean | number: '1.3-3' }}</td>
                          <td>{{ f.currentMean | number: '1.3-3' }}</td>
                          <td>
                            <app-status-badge
                              [variant]="deltaVariant(f.delta)"
                              [label]="deltaLabel(f.delta)"
                            />
                          </td>
                          <td>{{ f.pValue != null ? (f.pValue | number: '1.4-4') : '—' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                }

                @if (unverified().length > 0) {
                  <div class="unverified" data-testid="regression-unverified">
                    <p class="unverified__note">
                      Possible — the drop cleared the threshold but there isn't enough data to
                      confirm significance. Add more test cases to confirm.
                    </p>
                    <table class="sb-table">
                      <thead>
                        <tr>
                          <th>Scorer</th>
                          <th>From → To</th>
                          <th>Prior</th>
                          <th>Current</th>
                          <th>Δ</th>
                          <th>Confidence</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (f of unverified(); track f.scorer.identity + f.toVersionId) {
                          <tr data-testid="regression-unverified-row">
                            <td>{{ label(f) }}</td>
                            <td>v{{ f.fromVersionNumber }} → v{{ f.toVersionNumber }}</td>
                            <td>{{ f.priorMean | number: '1.3-3' }}</td>
                            <td>{{ f.currentMean | number: '1.3-3' }}</td>
                            <td>
                              <app-status-badge variant="warn" [label]="deltaLabel(f.delta)" />
                            </td>
                            <td><app-status-badge variant="warn" label="Possible" /></td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
              }
            }
          </app-card>

          <app-card heading="Compare versions">
            @if (versions().length < 2) {
              <app-empty-state
                message="Need at least two versions to compare."
                data-testid="compare-need-versions"
              />
            } @else {
              <p class="subtitle">
                Diff any two versions' content, scores, and judge rationale side by side.
              </p>
              <button
                type="button"
                class="sb-btn sb-btn--secondary"
                data-testid="open-compare"
                (click)="showCompare.set(true)"
              >
                Compare versions
              </button>
            }
          </app-card>
        </div>

        @if (showCompare()) {
          <app-compare-drawer
            [open]="true"
            [promptId]="promptId()!"
            [versions]="versions()"
            [datasetId]="datasetId()"
            [variance]="variance()"
            (closed)="showCompare.set(false)"
          />
        }
      } @else {
        <app-empty-state
          message="Choose a prompt and a dataset to see its score history."
          data-testid="prompt-choose"
        />
      }
    </section>
  `,
  styleUrls: ['../prompts/prompts.css', './analytics-dashboard.css'],
})
export class AnalyticsDashboard {
  private readonly api = inject(AnalyticsApiService);
  private readonly evalApi = inject(EvalRunsApiService);
  private readonly promptsApi = inject(PromptsApiService);
  private readonly datasetsApi = inject(DatasetsApiService);
  private readonly orgStore = inject(OrgContextStore);

  protected readonly prompts = signal<PromptSummary[]>([]);
  protected readonly datasets = signal<DatasetSummary[]>([]);
  protected readonly promptId = signal<string | null>(null);

  // B8: the dataset picker is scoped to the *selected prompt*, not just the org. A dataset belongs
  // to exactly one prompt (1.7, Dataset.PromptId), so a foreign-prompt dataset must never appear —
  // picking one yields empty/mismatched analytics. Same fix shape as the run picker (B3).
  protected readonly datasetsForPrompt = computed(() => {
    const pid = this.promptId();
    return pid ? this.datasets().filter((d) => d.promptId === pid) : [];
  });
  protected readonly datasetId = signal<string | null>(null);
  protected readonly threshold = signal<number>(0.05);
  protected readonly trends = signal<TrendSeries[]>([]);
  protected readonly composite = signal<CompositeTrendPoint[]>([]);
  protected readonly regressions = signal<RegressionFlag[] | null>(null);
  protected readonly error = signal<string | null>(null);

  // Split the flags: Confirmed (significant) render prominently; Unverified (threshold-clearing
  // but not statistically confirmed) render in a distinct, muted "possible" treatment.
  protected readonly confirmed = computed(() =>
    (this.regressions() ?? []).filter((f) => f.confidence === 'Confirmed'),
  );
  protected readonly unverified = computed(() =>
    (this.regressions() ?? []).filter((f) => f.confidence === 'Unverified'),
  );

  // Score stability (2.14/R4): mean ± spread over each version's repeated runs — so a run-to-run
  // wobble reads as spread, not signal. Aggregates ALL runs of a version (trends takes only latest).
  protected readonly variance = signal<ScorerVariance[]>([]);

  // The dataset's *current* scorer identities (2.19 W29). Editing a scorer's config mints a new
  // identity, so old runs accumulate stale identities that pollute trend/stability/compare. We
  // fetch the live set to mark those blocks stale (empty until loaded → nothing marked stale).
  private readonly currentScorerIds = signal<Set<string>>(new Set());
  protected readonly showHiddenVariance = signal(false);

  // W30: stability is only signal for *stochastic* (LLM-judge) scorers — deterministic scorers are
  // always ±0.000, pure noise here. Show the stochastic, current ones prominently; fold the rest
  // (deterministic and/or stale identities, W29) behind a reveal so the card stays scannable.
  protected readonly stochasticVariance = computed(() =>
    this.variance().filter((v) => isStochasticScorer(v.scorer.kind) && !this.isStale(v.scorer)),
  );
  protected readonly hiddenVariance = computed(() =>
    this.variance().filter((v) => !isStochasticScorer(v.scorer.kind) || this.isStale(v.scorer)),
  );

  // Trend lines from a removed/edited scorer config are stale phantoms (W29) — drop them so the
  // chart shows only the dataset's live scorers.
  protected readonly visibleTrends = computed(() =>
    this.trends().filter((s) => !this.isStale(s.scorer)),
  );

  // W31: the trend silently omits versions that have never run — "where's v4?". List them so the
  // gap is explicit. A version is un-run if no (live) trend series has a point for it.
  protected readonly unrunVersions = computed(() => {
    const run = new Set<string>();
    for (const s of this.visibleTrends()) for (const p of s.points) run.add(p.promptVersionId);
    return this.versions().filter((v) => !run.has(v.id));
  });
  protected unrunVersionsLabel(): string {
    return this.unrunVersions()
      .map((v) => `v${v.versionNumber}`)
      .join(', ');
  }

  protected isStale(scorer: ScorerRef): boolean {
    const ids = this.currentScorerIds();
    return ids.size > 0 && !ids.has(scorer.identity);
  }

  // Versions of the selected prompt — fed to the unified Compare drawer (W7), which owns the
  // From→To pick, the content/score/rationale tabs, and the cross-model / within-noise banners.
  protected readonly versions = signal<PromptVersion[]>([]);
  protected readonly showCompare = signal(false);

  // "0.750 ± 0.050" — a version's aggregate mean with its run-to-run spread.
  protected fmtStat(s: VarianceStat): string {
    return `${s.mean.toFixed(3)} ± ${s.stdDev.toFixed(3)}`;
  }

  // W29(a): label a scorer block by its config, not just kind. When the same kind+model appears
  // more than once (an edited config minted a second identity), append a short identity hash so the
  // otherwise-identical blocks are distinguishable; a lone block stays clean.
  protected scorerBlockLabel(scorer: ScorerRef): string {
    const base = scorerLabel(scorer);
    const duplicated = this.variance().filter((v) => scorerLabel(v.scorer) === base).length > 1;
    return duplicated ? `${base} · #${scorer.identity.slice(0, 6)}` : base;
  }

  constructor() {
    // Reload the org's prompts + datasets whenever the global org changes; clear the selection
    // (a prompt/dataset belongs to one org, so it can't carry across a switch).
    effect(() => {
      const orgId = this.orgStore.currentOrgId();
      this.promptId.set(null);
      this.datasetId.set(null);
      this.prompts.set([]);
      this.datasets.set([]);
      if (orgId) {
        this.loadOrgLists(orgId);
      }
    });

    // Reload trends + regressions whenever the prompt, dataset, or threshold changes.
    effect(() => {
      const promptId = this.promptId();
      const datasetId = this.datasetId();
      const threshold = this.threshold();
      if (promptId && datasetId) {
        this.load(promptId, datasetId, threshold);
      }
    });

    // Close the Compare drawer whenever the prompt/dataset changes out from under it.
    effect(() => {
      this.promptId();
      this.datasetId();
      this.showCompare.set(false);
    });
  }

  // Scope both lists to the active org: the org's prompts, and the datasets that belong to them
  // (datasets live with a prompt, so we intersect the cross-prompt list by the org's prompt ids).
  private loadOrgLists(orgId: string): void {
    forkJoin({
      prompts: this.promptsApi.listPromptsByOrganization(orgId),
      datasets: this.datasetsApi.listDatasets(),
    }).subscribe({
      next: ({ prompts, datasets }) => {
        // Ignore a response for an org we've since switched away from (stale-response race).
        if (this.orgStore.currentOrgId() !== orgId) return;
        const orgPromptIds = new Set(prompts.map((p) => p.id));
        this.prompts.set(prompts);
        this.datasets.set(datasets.filter((d) => orgPromptIds.has(d.promptId)));
      },
      error: () => this.error.set('Could not load prompts — is the stack running?'),
    });
  }

  protected label(flag: RegressionFlag): string {
    return scorerLabel(flag.scorer);
  }

  // A sharper drop reads as an error; a shallow one as a warning — both are flagged regressions.
  protected deltaVariant(delta: number): BadgeVariant {
    return delta <= -0.1 ? 'error' : 'warn';
  }

  protected deltaLabel(delta: number): string {
    return delta.toFixed(3);
  }

  // Selecting a prompt loads its versions (the Compare drawer seeds From→To from them).
  protected selectPrompt(promptId: string | null): void {
    this.promptId.set(promptId);
    // B8: drop a dataset selection that belongs to a different prompt — the picker is now
    // prompt-scoped, so a carried-over foreign dataset would analyze against the wrong prompt.
    const currentDataset = this.datasets().find((d) => d.id === this.datasetId());
    if (!promptId || (currentDataset && currentDataset.promptId !== promptId)) {
      this.datasetId.set(null);
    }
    this.versions.set([]);
    if (!promptId) {
      return;
    }
    this.promptsApi.getPrompt(promptId).subscribe({
      next: (prompt) => this.versions.set(prompt.versions),
      error: () => this.error.set('Could not load the prompt versions.'),
    });
  }

  private load(promptId: string, datasetId: string, threshold: number): void {
    this.error.set(null);
    this.api.getTrends(promptId, datasetId).subscribe({
      next: (series) => this.trends.set(series),
      error: () => this.error.set('Could not load trends.'),
    });
    this.api.getComposite(promptId, datasetId).subscribe({
      next: (points) => this.composite.set(points),
      error: () => this.error.set('Could not load the composite trend.'),
    });
    this.api.getRegressions(promptId, datasetId, threshold).subscribe({
      next: (flags) => this.regressions.set(flags),
      error: () => this.error.set('Could not load regressions.'),
    });
    this.api.getVariance(promptId, datasetId).subscribe({
      next: (v) => this.variance.set(v),
      error: () => this.error.set('Could not load score stability.'),
    });
    // W29: the dataset's live scorer set — used to mark stale identities. Best-effort: a failure
    // just means nothing is marked stale, never a blocked analytics view.
    this.showHiddenVariance.set(false);
    this.evalApi.listScorers(datasetId).subscribe({
      next: (scorers) => this.currentScorerIds.set(new Set(scorers.map((s) => s.identity))),
      error: () => this.currentScorerIds.set(new Set()),
    });
  }
}
