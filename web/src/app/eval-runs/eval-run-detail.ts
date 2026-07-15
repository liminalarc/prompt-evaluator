import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EvalRun } from '../eval-run';
import { PromptsApiService } from '../prompts/prompts-api.service';
import { DatasetsApiService } from '../datasets/datasets-api.service';
import {
  Breadcrumb,
  Chip,
  Crumb,
  EmptyState,
  ErrorState,
  LoadingState,
  PageHeader,
  StatusBadge,
  passBadge,
} from '../shared';
import { EvalRunsApiService } from './eval-runs-api.service';

@Component({
  selector: 'app-eval-run-detail',
  imports: [
    RouterLink,
    Breadcrumb,
    Chip,
    EmptyState,
    ErrorState,
    LoadingState,
    PageHeader,
    StatusBadge,
  ],
  template: `
    <section class="panel">
      <app-breadcrumb [items]="crumbs()" />

      @if (loading()) {
        <app-loading-state label="Loading run…" />
      } @else if (error(); as message) {
        <app-error-state [message]="message" />
      } @else if (run(); as r) {
        <app-page-header heading="Eval run" [subtitle]="summary(r)">
          <a
            actions
            class="sb-btn sb-btn--sm sb-btn--secondary"
            [routerLink]="['/datasets', r.datasetId]"
            >View dataset</a
          >
          <a
            actions
            class="sb-btn sb-btn--sm sb-btn--secondary"
            [routerLink]="['/prompts', r.promptId]"
            >View prompt</a
          >
        </app-page-header>

        @if (r.results.length === 0) {
          <app-empty-state message="This run has no fixture results." data-testid="no-results" />
        } @else {
          @for (fixtureRun of r.results; track fixtureRun.fixtureId) {
            <div class="sb-card fixture-run" data-testid="fixture-run">
              <div class="result__row">
                <span class="result__key">output</span> {{ fixtureRun.modelOutput }}
              </div>
              <div class="result__row">
                <span class="result__key">latency</span> {{ fixtureRun.latencyMs }}ms
                @if (fixtureRun.costUsd !== null) {
                  · <span class="result__key">cost</span> \${{ fixtureRun.costUsd }}
                }
              </div>

              <table class="sb-table" data-testid="scores">
                <thead>
                  <tr>
                    <th>Scorer</th>
                    <th>Judge model</th>
                    <th>Value</th>
                    <th>Passed</th>
                    <th>Detail</th>
                  </tr>
                </thead>
                <tbody>
                  @for (score of fixtureRun.scores; track score.scorerIdentity) {
                    <tr [attr.data-scorer]="score.scorerKind">
                      <td><app-chip [label]="score.scorerKind" /></td>
                      <td>
                        @if (score.judgeModel; as m) {
                          <app-chip [label]="m" />
                        } @else {
                          —
                        }
                      </td>
                      <td>{{ score.value }}</td>
                      <td>
                        <app-status-badge
                          [variant]="pass(score.passed).variant"
                          [label]="pass(score.passed).label"
                        />
                      </td>
                      <td>{{ score.detail ?? '—' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      }
    </section>
  `,
  styleUrl: '../prompts/prompts.css',
})
export class EvalRunDetail implements OnInit {
  private readonly api = inject(EvalRunsApiService);
  private readonly promptsApi = inject(PromptsApiService);
  private readonly datasetsApi = inject(DatasetsApiService);
  private readonly route = inject(ActivatedRoute);

  protected readonly run = signal<EvalRun | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly promptName = signal<string | null>(null);
  protected readonly datasetName = signal<string | null>(null);

  protected readonly pass = passBadge;

  // A run links back to both its prompt and its dataset (2.4) — the trail names them once loaded.
  protected readonly crumbs = computed<Crumb[]>(() => {
    const r = this.run();
    if (!r) return [{ label: 'Dashboard', link: '/' }];
    return [
      { label: 'Dashboard', link: '/' },
      { label: this.promptName() ?? 'Prompt', link: ['/prompts', r.promptId] },
      { label: this.datasetName() ?? 'Dataset', link: ['/datasets', r.datasetId] },
      { label: 'Eval run' },
    ];
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.getRun(id).subscribe({
      next: (r) => {
        this.run.set(r);
        this.loading.set(false);
        // Names for the breadcrumb + back-links; failures leave the generic labels in place.
        this.promptsApi
          .getPrompt(r.promptId)
          .subscribe({ next: (p) => this.promptName.set(p.name) });
        this.datasetsApi
          .getDataset(r.datasetId)
          .subscribe({ next: (d) => this.datasetName.set(d.name) });
      },
      error: () => {
        this.error.set('Could not load the eval run.');
        this.loading.set(false);
      },
    });
  }

  protected summary(run: EvalRun): string {
    return `${run.results.length} fixture(s) · ${this.scoreCount(run)} score(s)`;
  }

  protected scoreCount(run: EvalRun): number {
    return run.results.reduce((total, fixtureRun) => total + fixtureRun.scores.length, 0);
  }
}
