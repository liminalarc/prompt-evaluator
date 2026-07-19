import { Component, computed, effect, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import {
  RegressionFlag,
  ScorerVariance,
  TrendSeries,
  VarianceStat,
  VersionComparison as VersionComparisonData,
  scorerLabel,
} from '../analytics';
import { AnalyticsApiService } from './analytics-api.service';
import { PromptsApiService } from '../prompts/prompts-api.service';
import { DatasetsApiService } from '../datasets/datasets-api.service';
import { OrgContextStore } from '../shared/org-context.store';
import { PromptSummary, PromptVersion } from '../prompt';
import { DatasetSummary } from '../dataset';
import { TrendChart } from './trend-chart';
import { VersionComparison } from './version-comparison';
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
    TrendChart,
    VersionComparison,
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
          <app-trend-chart [series]="trends()" />
        </app-card>

        @if (variance().length > 0) {
          <app-card heading="Score stability">
            <p class="subtitle">
              Mean ± spread over each version's <strong>repeated runs</strong> — a run-to-run wobble
              is noise, not signal. Run a version more than once to build the distribution.
            </p>
            @for (sv of variance(); track sv.scorer.identity) {
              <div class="variance-block" data-testid="variance-scorer">
                <h3 class="scorer-title">{{ varianceLabel(sv) }}</h3>
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
              </div>
            }
          </app-card>
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
                      confirm significance. Add more fixtures to confirm.
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
              <form class="selectors">
                <div class="sb-field">
                  <label for="from">From</label>
                  <select
                    id="from"
                    name="from"
                    data-testid="from-version"
                    [ngModel]="fromVersionId()"
                    (ngModelChange)="fromVersionId.set($event)"
                  >
                    @for (v of versions(); track v.id) {
                      <option [ngValue]="v.id">{{ versionLabel(v) }}</option>
                    }
                  </select>
                </div>
                <div class="sb-field">
                  <label for="to">To</label>
                  <select
                    id="to"
                    name="to"
                    data-testid="to-version"
                    [ngModel]="toVersionId()"
                    (ngModelChange)="toVersionId.set($event)"
                  >
                    @for (v of versions(); track v.id) {
                      <option [ngValue]="v.id">{{ versionLabel(v) }}</option>
                    }
                  </select>
                </div>
              </form>
              @if (crossModelCompare(); as cm) {
                <p class="cross-model-warn" data-testid="cross-model-warning">
                  ⚠ These versions ran on different subject models ({{ cm.from }} vs {{ cm.to }}). A
                  score delta here mixes the prompt change with a model change — hold the subject
                  model constant to compare the prompt cleanly.
                </p>
              }
              @if (compareNoise(); as noise) {
                @if (noise.withinNoise) {
                  <p class="cross-model-warn" data-testid="within-noise">
                    ⚠ This change (Δ {{ noise.delta.toFixed(3) }}) is within run-to-run noise (±{{
                      noise.spread.toFixed(3)
                    }}) — not a confident move. Run more repeats to confirm.
                  </p>
                }
              }
              <app-version-comparison [comparison]="comparison()" />
            }
          </app-card>
        </div>
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

  protected readonly versions = signal<PromptVersion[]>([]);
  protected readonly fromVersionId = signal<string | null>(null);
  protected readonly toVersionId = signal<string | null>(null);
  protected readonly comparison = signal<VersionComparisonData | null>(null);

  // Flag a version-over-version change that sits within run-to-run noise: if |Δ| for the primary
  // scorer is within the two versions' combined spread, it isn't a confident move (R4). Needs
  // repeated runs to mean anything — with one run each, spread is 0 and nothing is flagged.
  protected readonly compareNoise = computed<{
    withinNoise: boolean;
    spread: number;
    delta: number;
  } | null>(() => {
    const cmp = this.comparison();
    const from = this.fromVersionId();
    const to = this.toVersionId();
    if (!cmp || !from || !to || cmp.scorers.length === 0) return null;
    const sc = cmp.scorers[0];
    if (sc.delta == null) return null;
    const sv = this.variance().find((v) => v.scorer.identity === sc.scorer.identity);
    const fromVar = sv?.versions.find((v) => v.promptVersionId === from);
    const toVar = sv?.versions.find((v) => v.promptVersionId === to);
    if (!fromVar || !toVar) return null;
    const spread = fromVar.aggregate.stdDev + toVar.aggregate.stdDev;
    if (spread <= 0) return null; // no repeated runs → nothing to say
    return { withinNoise: Math.abs(sc.delta) <= spread, spread, delta: sc.delta };
  });

  // "0.750 ± 0.050" — a version's aggregate mean with its run-to-run spread.
  protected fmtStat(s: VarianceStat): string {
    return `${s.mean.toFixed(3)} ± ${s.stdDev.toFixed(3)}`;
  }

  protected varianceLabel(sv: ScorerVariance): string {
    return scorerLabel(sv.scorer);
  }

  // R5: flag a cross-model comparison. If the two selected versions ran on different subject models,
  // a score delta mixes the prompt change with a model change — you can't cleanly attribute it to
  // the prompt. Sibling to holding the model on add-version and to 1.16's same-scorer-config rule.
  protected readonly crossModelCompare = computed<{ from: string; to: string } | null>(() => {
    const from = this.versions().find((v) => v.id === this.fromVersionId());
    const to = this.versions().find((v) => v.id === this.toVersionId());
    if (!from || !to || from.id === to.id) return null;
    return from.targetModel !== to.targetModel
      ? { from: from.targetModel, to: to.targetModel }
      : null;
  });

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

    // Reload the comparison whenever the prompt, dataset, or either version selection changes.
    effect(() => {
      const promptId = this.promptId();
      const datasetId = this.datasetId();
      const from = this.fromVersionId();
      const to = this.toVersionId();
      if (promptId && datasetId && from && to && from !== to) {
        this.loadComparison(promptId, datasetId, from, to);
      } else {
        this.comparison.set(null);
      }
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

  protected versionLabel(version: PromptVersion): string {
    return version.label
      ? `v${version.versionNumber} · ${version.label}`
      : `v${version.versionNumber}`;
  }

  // Selecting a prompt loads its versions and defaults the comparison to the two most recent.
  protected selectPrompt(promptId: string | null): void {
    this.promptId.set(promptId);
    // B8: drop a dataset selection that belongs to a different prompt — the picker is now
    // prompt-scoped, so a carried-over foreign dataset would analyze against the wrong prompt.
    const currentDataset = this.datasets().find((d) => d.id === this.datasetId());
    if (!promptId || (currentDataset && currentDataset.promptId !== promptId)) {
      this.datasetId.set(null);
    }
    this.versions.set([]);
    this.fromVersionId.set(null);
    this.toVersionId.set(null);
    if (!promptId) {
      return;
    }
    this.promptsApi.getPrompt(promptId).subscribe({
      next: (prompt) => {
        this.versions.set(prompt.versions);
        const n = prompt.versions.length;
        if (n >= 2) {
          this.fromVersionId.set(prompt.versions[n - 2].id);
          this.toVersionId.set(prompt.versions[n - 1].id);
        }
      },
      error: () => this.error.set('Could not load the prompt versions.'),
    });
  }

  private load(promptId: string, datasetId: string, threshold: number): void {
    this.error.set(null);
    this.api.getTrends(promptId, datasetId).subscribe({
      next: (series) => this.trends.set(series),
      error: () => this.error.set('Could not load trends.'),
    });
    this.api.getRegressions(promptId, datasetId, threshold).subscribe({
      next: (flags) => this.regressions.set(flags),
      error: () => this.error.set('Could not load regressions.'),
    });
    this.api.getVariance(promptId, datasetId).subscribe({
      next: (v) => this.variance.set(v),
      error: () => this.error.set('Could not load score stability.'),
    });
  }

  private loadComparison(promptId: string, datasetId: string, from: string, to: string): void {
    this.api.getComparison(promptId, datasetId, from, to).subscribe({
      next: (cmp) => this.comparison.set(cmp),
      error: () => this.error.set('Could not load the version comparison.'),
    });
  }
}
