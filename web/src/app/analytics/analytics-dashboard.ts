import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RegressionFlag, TrendSeries, scorerLabel } from '../analytics';
import { AnalyticsApiService } from './analytics-api.service';
import { PromptsApiService } from '../prompts/prompts-api.service';
import { DatasetsApiService } from '../datasets/datasets-api.service';
import { PromptSummary } from '../prompt';
import { DatasetSummary } from '../dataset';
import { TrendChart } from './trend-chart';

/**
 * Score-tracking dashboard: pick a prompt + dataset, then see the score trend across versions
 * (one line per scorer) and the regression flags between consecutive versions.
 */
@Component({
  selector: 'app-analytics-dashboard',
  imports: [FormsModule, DecimalPipe, TrendChart],
  template: `
    <section class="panel">
      <header class="panel__head">
        <h1 class="title">Score Analytics</h1>
        <p class="subtitle">
          Trends and regressions per <code>Prompt × Version × Dataset × Scorer</code>.
        </p>
      </header>

      <form class="selectors">
        <div class="sb-field">
          <label for="prompt">Prompt</label>
          <select
            id="prompt"
            name="prompt"
            data-testid="prompt-select"
            [ngModel]="promptId()"
            (ngModelChange)="promptId.set($event)"
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
            @for (d of datasets(); track d.id) {
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

      @if (error(); as message) {
        <div class="error-box" data-testid="error">{{ message }}</div>
      }

      @if (promptId() && datasetId()) {
        <div class="card chart-card">
          <h2 class="section-title">Score trend</h2>
          <app-trend-chart [series]="trends()" />
        </div>

        <div class="card">
          <h2 class="section-title">Regressions</h2>
          @if (regressions(); as flags) {
            @if (flags.length === 0) {
              <p class="empty" data-testid="no-regressions">
                No regressions beyond the threshold. 🎉
              </p>
            } @else {
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
                  @for (f of flags; track f.scorer.identity + f.toVersionId) {
                    <tr data-testid="regression-row">
                      <td>{{ label(f) }}</td>
                      <td>v{{ f.fromVersionNumber }} → v{{ f.toVersionNumber }}</td>
                      <td>{{ f.priorMean | number: '1.3-3' }}</td>
                      <td>{{ f.currentMean | number: '1.3-3' }}</td>
                      <td class="delta-down">⚠ {{ f.delta | number: '1.3-3' }}</td>
                      <td>{{ f.pValue != null ? (f.pValue | number: '1.4-4') : '—' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          }
        </div>
      } @else {
        <p class="empty" data-testid="prompt-choose">
          Choose a prompt and a dataset to see its score history.
        </p>
      }
    </section>
  `,
  styleUrls: ['../prompts/prompts.css', './analytics-dashboard.css'],
})
export class AnalyticsDashboard implements OnInit {
  private readonly api = inject(AnalyticsApiService);
  private readonly promptsApi = inject(PromptsApiService);
  private readonly datasetsApi = inject(DatasetsApiService);

  protected readonly prompts = signal<PromptSummary[]>([]);
  protected readonly datasets = signal<DatasetSummary[]>([]);
  protected readonly promptId = signal<string | null>(null);
  protected readonly datasetId = signal<string | null>(null);
  protected readonly threshold = signal<number>(0.05);
  protected readonly trends = signal<TrendSeries[]>([]);
  protected readonly regressions = signal<RegressionFlag[] | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // Reload whenever the prompt, dataset, or threshold changes.
    effect(() => {
      const promptId = this.promptId();
      const datasetId = this.datasetId();
      const threshold = this.threshold();
      if (promptId && datasetId) {
        this.load(promptId, datasetId, threshold);
      }
    });
  }

  ngOnInit(): void {
    this.promptsApi.listPrompts().subscribe({
      next: (list) => this.prompts.set(list),
      error: () => this.error.set('Could not load prompts — is the stack running?'),
    });
    this.datasetsApi.listDatasets().subscribe({
      next: (list) => this.datasets.set(list),
      error: () => this.error.set('Could not load datasets — is the stack running?'),
    });
  }

  protected label(flag: RegressionFlag): string {
    return scorerLabel(flag.scorer);
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
}
