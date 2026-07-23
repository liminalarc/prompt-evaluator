import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EvalRun, FixtureRunView, ScoreView } from '../eval-run';
import { Fixture } from '../dataset';
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
    <section class="panel panel--wide">
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
          <app-empty-state message="This run has no test case results." data-testid="no-results" />
        } @else {
          @for (fixtureRun of r.results; track fixtureRun.fixtureId) {
            <div class="sb-card fixture-run" data-testid="fixture-run">
              <button
                type="button"
                class="fixture-run__summary"
                (click)="toggle(fixtureRun.fixtureId)"
                data-testid="fixture-run-summary"
              >
                <span class="fixture-run__label">{{ fixtureLabel(fixtureRun.fixtureId) }}</span>
                <span class="fixture-run__scores">
                  @for (score of orderedScores(fixtureRun); track score.scorerIdentity) {
                    <app-status-badge
                      [variant]="pass(score.passed).variant"
                      [label]="score.scorerKind + ' ' + score.value"
                    />
                  } @empty {
                    <span class="result__key">no scores</span>
                  }
                </span>
                <span class="fixture-run__chevron">{{
                  isOpen(fixtureRun.fixtureId) ? '▾' : '▸'
                }}</span>
              </button>
              @if (isOpen(fixtureRun.fixtureId)) {
                <div class="sb-card__body" data-testid="fixture-run-detail">
                  <div class="io-grid">
                    <div class="io-block">
                      <span class="result__key">Test case input</span>
                      <pre class="io-text">{{ fixtureInput(fixtureRun.fixtureId) }}</pre>
                    </div>
                    <div class="io-block">
                      <span class="result__key">Model output</span>
                      <pre class="io-text io-text--output">{{
                        formatOutput(fixtureRun.modelOutput)
                      }}</pre>
                    </div>
                  </div>

                  <dl class="result__meta">
                    <div class="result__row">
                      <dt class="result__key">Latency</dt>
                      <dd class="result__val">{{ fixtureRun.latencyMs }} ms</dd>
                    </div>
                    <div class="result__row">
                      <dt class="result__key">Input tokens</dt>
                      <dd class="result__val" data-testid="input-tokens">
                        {{ fixtureRun.inputTokens }}
                      </dd>
                    </div>
                    <div class="result__row">
                      <dt class="result__key">Output tokens</dt>
                      <dd class="result__val" data-testid="output-tokens">
                        {{ fixtureRun.outputTokens }}
                      </dd>
                    </div>
                    <div class="result__row">
                      <dt class="result__key">Cost</dt>
                      <dd class="result__val">
                        {{ fixtureRun.costUsd !== null ? '$' + fixtureRun.costUsd : '—' }}
                      </dd>
                    </div>
                  </dl>

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
                      @for (score of orderedScores(fixtureRun); track score.scorerIdentity) {
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
            </div>
          }
        }
      }
    </section>
  `,
  styleUrl: '../prompts/prompts.css',
  styles: [
    `
      .fixture-run + .fixture-run {
        margin-top: var(--sb-space-md);
      }
      .fixture-run__summary {
        display: flex;
        align-items: center;
        gap: var(--sb-space-md);
        width: 100%;
        padding: var(--sb-space-md);
        background: transparent;
        border: none;
        cursor: pointer;
        text-align: left;
        color: var(--sb-text);
      }
      .fixture-run__label {
        font-weight: 600;
        flex: 1;
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .fixture-run__scores {
        display: flex;
        flex-wrap: wrap;
        gap: var(--sb-space-xs);
      }
      .fixture-run__chevron {
        color: var(--sb-text-muted);
      }
      .io-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
        gap: var(--sb-space-lg);
        margin-bottom: var(--sb-space-lg);
      }
      .io-block {
        display: flex;
        flex-direction: column;
        gap: var(--sb-space-xs);
        min-width: 0;
      }
      .io-text {
        margin: 0;
        font-family: var(--sb-font-mono);
        font-size: var(--sb-type-small-size);
        line-height: 1.5;
        color: var(--sb-text);
        background: var(--sb-surface-variant);
        border: 1px solid var(--sb-border);
        border-radius: var(--sb-radius-md);
        padding: var(--sb-space-md);
        max-height: 420px;
        overflow: auto;
        /* W28: draggable taller — the boxes were a cramped fixed scroll. */
        resize: vertical;
        white-space: pre-wrap;
        word-break: break-word;
      }
      /* W28: the model output is the primary artifact in this focused view — give it more room. */
      .io-text--output {
        max-height: 640px;
      }
      .result__meta {
        display: flex;
        flex-wrap: wrap;
        gap: var(--sb-space-xl);
        margin: 0 0 var(--sb-space-lg);
      }
      .result__row {
        display: flex;
        flex-direction: column;
        gap: 2px;
      }
      .result__key {
        font-size: var(--sb-type-caption-size);
        font-weight: var(--sb-type-caption-weight);
        letter-spacing: 0.05em;
        text-transform: uppercase;
        color: var(--sb-text-muted);
      }
      .result__val {
        margin: 0;
        font-variant-numeric: tabular-nums;
        color: var(--sb-text);
      }
    `,
  ],
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
  // The run DTO carries only fixtureId per result; the dataset supplies each fixture's input text
  // so we can show the input alongside its output.
  protected readonly fixturesById = signal<Map<string, Fixture>>(new Map());
  // Progressive disclosure (U10): each fixture result collapses to a summary row; expand for detail.
  protected readonly expanded = signal<Set<string>>(new Set());

  protected readonly pass = passBadge;

  protected isOpen(fixtureId: string): boolean {
    return this.expanded().has(fixtureId);
  }

  protected toggle(fixtureId: string): void {
    const next = new Set(this.expanded());
    next.has(fixtureId) ? next.delete(fixtureId) : next.add(fixtureId);
    this.expanded.set(next);
  }

  /**
   * U21: render a fixture-run's scores in a stable order on every row (the API doesn't guarantee an
   * order), so the badge/table columns line up for scanning. Sort by scorer kind, then by the stable
   * scorer identity as a tie-break. Returns a sorted copy — never mutates the run.
   */
  protected orderedScores(fixtureRun: FixtureRunView): ScoreView[] {
    return [...fixtureRun.scores].sort(
      (a, b) =>
        a.scorerKind.localeCompare(b.scorerKind) ||
        a.scorerIdentity.localeCompare(b.scorerIdentity),
    );
  }

  /** Fixture label if it has one, else a short form of its input, else the id — for the summary row. */
  protected fixtureLabel(fixtureId: string): string {
    const f = this.fixturesById().get(fixtureId);
    if (f?.label) return f.label;
    if (f?.input) return f.input.length > 60 ? f.input.slice(0, 60) + '…' : f.input;
    return fixtureId.slice(0, 8);
  }

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
        this.datasetsApi.getDataset(r.datasetId).subscribe({
          next: (d) => {
            this.datasetName.set(d.name);
            this.fixturesById.set(new Map(d.fixtures.map((f) => [f.id, f])));
          },
        });
      },
      error: () => {
        this.error.set('Could not load the eval run.');
        this.loading.set(false);
      },
    });
  }

  protected summary(run: EvalRun): string {
    const base = `${run.results.length} test case(s) · ${this.scoreCount(run)} score(s)`;
    const mean = this.meanScore(run);
    return mean ? `${base} · mean ${mean.value.toFixed(2)}${mean.judge ? ' (judge)' : ''}` : base;
  }

  protected scoreCount(run: EvalRun): number {
    return run.results.reduce((total, fixtureRun) => total + fixtureRun.scores.length, 0);
  }

  // The run's meaningful headline mean (W27): the LLM-judge scores when present (deterministic
  // scorers are near-always 1.0 and would inflate an overall mean), else the overall mean.
  private meanScore(run: EvalRun): { value: number; judge: boolean } | null {
    const all = run.results.flatMap((fr) => fr.scores);
    if (all.length === 0) return null;
    const judge = all.filter((s) => s.scorerKind === 'LlmJudge');
    const chosen = judge.length > 0 ? judge : all;
    const value = chosen.reduce((sum, s) => sum + s.value, 0) / chosen.length;
    return { value, judge: judge.length > 0 };
  }

  /** The captured/synthetic input the model was run over; '—' until the dataset resolves. */
  protected fixtureInput(fixtureId: string): string {
    return this.fixturesById().get(fixtureId)?.input ?? '—';
  }

  /**
   * Model output is often returned wrapped in a ```json … ``` markdown fence. Strip the fence and
   * pretty-print when the payload parses as JSON; otherwise show the plain text as-is.
   */
  protected formatOutput(raw: string): string {
    const stripped = this.stripFence(raw ?? '');
    try {
      return JSON.stringify(JSON.parse(stripped), null, 2);
    } catch {
      return stripped;
    }
  }

  private stripFence(raw: string): string {
    const trimmed = raw.trim();
    const fenced = trimmed.match(/^```(?:json)?\s*\n?([\s\S]*?)\n?```$/i);
    return (fenced ? fenced[1] : trimmed).trim();
  }
}
