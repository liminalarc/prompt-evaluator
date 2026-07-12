import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EvalRun } from '../eval-run';
import { EvalRunsApiService } from './eval-runs-api.service';

@Component({
  selector: 'app-eval-run-detail',
  imports: [RouterLink],
  template: `
    <section class="panel">
      <a class="back" routerLink="/datasets">← All datasets</a>

      @if (error(); as message) {
        <div class="error-box" data-testid="error">{{ message }}</div>
      }

      @if (run(); as r) {
        <header class="panel__head">
          <h1 class="title">Eval run</h1>
          <p class="subtitle">
            {{ r.results.length }} fixture(s) · {{ scoreCount(r) }} score(s) ·
            <a [routerLink]="['/datasets', r.datasetId]">dataset</a>
          </p>
        </header>

        @if (r.results.length === 0) {
          <p class="empty" data-testid="no-results">This run has no fixture results.</p>
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
                      <td>{{ score.scorerKind }}</td>
                      <td>{{ score.judgeModel ?? '—' }}</td>
                      <td>{{ score.value }}</td>
                      <td>{{ score.passed === null ? '—' : score.passed ? '✅' : '❌' }}</td>
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
  private readonly route = inject(ActivatedRoute);

  protected readonly run = signal<EvalRun | null>(null);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.getRun(id).subscribe({
      next: (r) => this.run.set(r),
      error: () => this.error.set('Could not load the eval run.'),
    });
  }

  protected scoreCount(run: EvalRun): number {
    return run.results.reduce((total, fixtureRun) => total + fixtureRun.scores.length, 0);
  }
}
