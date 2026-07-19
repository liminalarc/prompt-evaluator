import { Component, computed, effect, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import {
  RegressionFlag,
  TrendSeries,
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

  protected readonly versions = signal<PromptVersion[]>([]);
  protected readonly fromVersionId = signal<string | null>(null);
  protected readonly toVersionId = signal<string | null>(null);
  protected readonly comparison = signal<VersionComparisonData | null>(null);

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
  }

  private loadComparison(promptId: string, datasetId: string, from: string, to: string): void {
    this.api.getComparison(promptId, datasetId, from, to).subscribe({
      next: (cmp) => this.comparison.set(cmp),
      error: () => this.error.set('Could not load the version comparison.'),
    });
  }
}
