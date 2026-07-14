import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Dataset } from '../dataset';
import { EvalRunSummary, SCORER_KINDS, ScorerConfig, ScorerKind } from '../eval-run';
import { Prompt, PromptSummary, PromptVersion } from '../prompt';
import { EvalRunsApiService } from '../eval-runs/eval-runs-api.service';
import { PromptsApiService } from '../prompts/prompts-api.service';
import {
  Breadcrumb,
  Chip,
  Crumb,
  EmptyState,
  ErrorState,
  LoadingState,
  PageHeader,
  StatusBadge,
  originBadge,
} from '../shared';
import { DatasetsApiService } from './datasets-api.service';

type OriginFilter = 'all' | 'Captured' | 'Synthetic';

const JUDGE_MODELS = ['claude-opus-4-8', 'claude-sonnet-5', 'claude-haiku-4-5'];

@Component({
  selector: 'app-dataset-detail',
  imports: [
    FormsModule,
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

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      @if (loading()) {
        <app-loading-state label="Loading dataset…" />
      } @else if (dataset(); as d) {
        <app-page-header [heading]="d.name" [subtitle]="d.description ?? ''" />

        <h2 class="section-title">Fixtures</h2>
        <div class="filter">
          <label for="origin">Origin</label>
          <select
            id="origin"
            [ngModel]="originFilter()"
            (ngModelChange)="originFilter.set($event)"
            data-testid="origin-filter"
          >
            <option value="all">All</option>
            <option value="Captured">Captured</option>
            <option value="Synthetic">Synthetic</option>
          </select>
        </div>

        @if (filteredFixtures().length === 0) {
          <app-empty-state message="No fixtures for this filter." data-testid="no-fixtures" />
        } @else {
          <table class="sb-table" data-testid="fixtures">
            <thead>
              <tr>
                <th>Origin</th>
                <th>Input</th>
                <th>Upstream context</th>
                <th>Expected output</th>
                <th>Seed</th>
              </tr>
            </thead>
            <tbody>
              @for (f of filteredFixtures(); track f.id) {
                <tr [attr.data-origin]="f.origin">
                  <td>
                    <app-status-badge
                      [variant]="originBadge(f.origin).variant"
                      [label]="originBadge(f.origin).label"
                    />
                  </td>
                  <td>{{ f.input }}</td>
                  <td>{{ f.upstreamContext ?? '—' }}</td>
                  <td>{{ f.expectedOutput ?? '—' }}</td>
                  <td>{{ f.seedFixtureId ? 'linked' : '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }

        <div class="toolbar">
          <button
            class="sb-btn sb-btn--sm"
            type="button"
            data-testid="toggle-capture"
            (click)="showCapture.set(!showCapture())"
          >
            + Capture fixture
          </button>
          <button
            class="sb-btn sb-btn--sm"
            type="button"
            data-testid="toggle-generate"
            (click)="showGenerate.set(!showGenerate())"
          >
            + Generate synthetic
          </button>
        </div>

        @if (showCapture()) {
          <form class="capture reveal" (submit)="capture($event)">
            <div class="sb-field">
              <label for="promptInput">Prompt input</label>
              <textarea
                id="promptInput"
                name="promptInput"
                rows="2"
                [ngModel]="promptInput()"
                (ngModelChange)="promptInput.set($event)"
              ></textarea>
            </div>
            <div class="sb-field">
              <label for="slmOutput">Upstream SLM output (optional)</label>
              <textarea
                id="slmOutput"
                name="slmOutput"
                rows="2"
                [ngModel]="slmOutput()"
                (ngModelChange)="slmOutput.set($event)"
              ></textarea>
            </div>
            <button class="sb-btn sb-btn--primary" type="submit" data-testid="capture">
              Capture fixture
            </button>
          </form>
        }

        @if (showGenerate()) {
          <p class="subtitle">Seeded from this dataset's captured fixtures; steer with guidance.</p>
          <form class="generate reveal" (submit)="generate($event)">
            <div class="sb-field">
              <label for="coverageGoals">Coverage goals (optional)</label>
              <input
                id="coverageGoals"
                name="coverageGoals"
                [ngModel]="coverageGoals()"
                (ngModelChange)="coverageGoals.set($event)"
              />
            </div>
            <div class="sb-field">
              <label for="edgeCases">Edge cases / adversarial (optional)</label>
              <input
                id="edgeCases"
                name="edgeCases"
                [ngModel]="edgeCases()"
                (ngModelChange)="edgeCases.set($event)"
              />
            </div>
            <div class="sb-field">
              <label for="count">Count</label>
              <input
                id="count"
                name="count"
                type="number"
                min="1"
                [ngModel]="count()"
                (ngModelChange)="count.set(+$event)"
              />
            </div>
            <button
              class="sb-btn sb-btn--primary"
              type="submit"
              data-testid="generate"
              [disabled]="generating()"
            >
              {{ generating() ? 'Generating…' : 'Generate' }}
            </button>
          </form>
        }

        <h2 class="section-title">Scorers</h2>
        <p class="subtitle">Configured once per dataset; every run scores with this set.</p>
        @if (scorers().length === 0) {
          <app-empty-state message="No scorers configured yet." data-testid="no-scorers" />
        } @else {
          <table class="sb-table" data-testid="scorers">
            <thead>
              <tr>
                <th>Kind</th>
                <th>Config</th>
                <th>Judge model</th>
              </tr>
            </thead>
            <tbody>
              @for (s of scorers(); track s.id) {
                <tr [attr.data-scorer]="s.kind">
                  <td><app-chip [label]="s.kind" /></td>
                  <td>{{ s.config || '—' }}</td>
                  <td>
                    @if (s.judgeModel; as m) {
                      <app-chip [label]="m" />
                    } @else {
                      —
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
        <div class="toolbar">
          <button
            class="sb-btn sb-btn--sm"
            type="button"
            data-testid="toggle-add-scorer"
            (click)="showAddScorer.set(!showAddScorer())"
          >
            + Add scorer
          </button>
        </div>
        @if (showAddScorer()) {
          <form class="add-scorer reveal" (submit)="addScorer($event)">
            <div class="sb-field">
              <label for="scorerKind">Scorer</label>
              <select
                id="scorerKind"
                name="scorerKind"
                [ngModel]="scorerKind()"
                (ngModelChange)="scorerKind.set($event)"
                data-testid="scorer-kind"
              >
                @for (k of scorerKinds; track k) {
                  <option [value]="k">{{ k }}</option>
                }
              </select>
            </div>
            <div class="sb-field">
              <label for="scorerConfig">{{ isJudge() ? 'Rubric' : 'Config (optional)' }}</label>
              <input
                id="scorerConfig"
                name="scorerConfig"
                [ngModel]="scorerConfig()"
                (ngModelChange)="scorerConfig.set($event)"
                data-testid="scorer-config"
              />
            </div>
            @if (isJudge()) {
              <div class="sb-field">
                <label for="judgeModel">Judge model</label>
                <select
                  id="judgeModel"
                  name="judgeModel"
                  [ngModel]="judgeModel()"
                  (ngModelChange)="judgeModel.set($event)"
                  data-testid="judge-model"
                >
                  @for (m of judgeModels; track m) {
                    <option [value]="m">{{ m }}</option>
                  }
                </select>
              </div>
            }
            <button class="sb-btn sb-btn--primary" type="submit" data-testid="add-scorer">
              Add scorer
            </button>
          </form>
        }

        <h2 class="section-title">Run evaluation</h2>
        <form class="run" (submit)="triggerRun($event)">
          <div class="sb-field">
            <label for="promptSelect">Prompt</label>
            <select
              id="promptSelect"
              name="promptSelect"
              [ngModel]="selectedPromptId()"
              (ngModelChange)="onPromptChange($event)"
              data-testid="prompt-select"
            >
              <option value="">Select a prompt…</option>
              @for (p of prompts(); track p.id) {
                <option [value]="p.id">{{ p.name }}</option>
              }
            </select>
          </div>
          @if (versions().length > 0) {
            <div class="sb-field">
              <label for="versionSelect">Version</label>
              <select
                id="versionSelect"
                name="versionSelect"
                [ngModel]="selectedVersionId()"
                (ngModelChange)="selectedVersionId.set($event)"
                data-testid="version-select"
              >
                @for (v of versions(); track v.id) {
                  <option [value]="v.id">v{{ v.versionNumber }} · {{ v.targetModel }}</option>
                }
              </select>
            </div>
          }
          <button
            class="sb-btn sb-btn--primary"
            type="submit"
            data-testid="run"
            [disabled]="running() || !selectedVersionId()"
          >
            {{ running() ? 'Running…' : 'Run evaluation' }}
          </button>
        </form>

        <h2 class="section-title">Runs</h2>
        @if (runs().length === 0) {
          <app-empty-state message="No runs yet." data-testid="no-runs" />
        } @else {
          <ul class="runs" data-testid="runs">
            @for (r of runs(); track r.id) {
              <li>
                <a [routerLink]="['/eval-runs', r.id]" data-testid="run-link">
                  {{ r.createdAt }} — {{ r.fixtureCount }} fixture(s), {{ r.scoreCount }} score(s)
                </a>
              </li>
            }
          </ul>
        }
      }
    </section>
  `,
  styleUrl: '../prompts/prompts.css',
})
export class DatasetDetail implements OnInit {
  private readonly api = inject(DatasetsApiService);
  private readonly evalApi = inject(EvalRunsApiService);
  private readonly promptsApi = inject(PromptsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly scorerKinds = SCORER_KINDS;
  protected readonly judgeModels = JUDGE_MODELS;

  protected readonly dataset = signal<Dataset | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly promptName = signal<string | null>(null);
  protected readonly originFilter = signal<OriginFilter>('all');
  protected readonly generating = signal(false);

  // Progressive disclosure: data tables stay visible, creation forms are revealed on demand.
  protected readonly showCapture = signal(false);
  protected readonly showGenerate = signal(false);
  protected readonly showAddScorer = signal(false);

  protected readonly originBadge = originBadge;

  protected readonly promptInput = signal('');
  protected readonly slmOutput = signal('');
  protected readonly coverageGoals = signal('');
  protected readonly edgeCases = signal('');
  protected readonly count = signal(5);

  protected readonly scorers = signal<ScorerConfig[]>([]);
  protected readonly scorerKind = signal<ScorerKind>('Regex');
  protected readonly scorerConfig = signal('');
  protected readonly judgeModel = signal(JUDGE_MODELS[0]);

  protected readonly prompts = signal<PromptSummary[]>([]);
  protected readonly versions = signal<PromptVersion[]>([]);
  protected readonly selectedPromptId = signal('');
  protected readonly selectedVersionId = signal('');
  protected readonly running = signal(false);

  protected readonly runs = signal<EvalRunSummary[]>([]);

  private id = '';

  protected readonly filteredFixtures = computed(() => {
    const fixtures = this.dataset()?.fixtures ?? [];
    const filter = this.originFilter();
    return filter === 'all' ? fixtures : fixtures.filter((f) => f.origin === filter);
  });

  protected readonly isJudge = computed(() => this.scorerKind() === 'LlmJudge');

  // A dataset lives with a prompt (1.7) — the trail leads back through its owning prompt workspace.
  protected readonly crumbs = computed<Crumb[]>(() => {
    const d = this.dataset();
    if (!d) return [{ label: 'Dashboard', link: '/' }];
    return [
      { label: 'Dashboard', link: '/' },
      { label: this.promptName() ?? 'Prompt', link: ['/prompts', d.promptId] },
      { label: d.name },
    ];
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
    this.loadScorers();
    this.loadRuns();
    this.promptsApi.listPrompts().subscribe({ next: (p) => this.prompts.set(p) });
  }

  private load(): void {
    this.api.getDataset(this.id).subscribe({
      next: (d) => {
        this.dataset.set(d);
        this.loading.set(false);
        this.promptsApi
          .getPrompt(d.promptId)
          .subscribe({ next: (p) => this.promptName.set(p.name) });
      },
      error: () => {
        this.error.set('Could not load the dataset.');
        this.loading.set(false);
      },
    });
  }

  private loadScorers(): void {
    this.evalApi.listScorers(this.id).subscribe({ next: (s) => this.scorers.set(s) });
  }

  private loadRuns(): void {
    this.evalApi.listRuns(this.id).subscribe({ next: (r) => this.runs.set(r) });
  }

  protected capture(event: Event): void {
    event.preventDefault();
    const promptInput = this.promptInput().trim();
    if (!promptInput) {
      return;
    }
    this.error.set(null);
    this.api
      .captureFixtures(this.id, [
        {
          promptInput,
          input: null,
          slmOutput: this.slmOutput().trim() || null,
          downstreamResult: null,
        },
      ])
      .subscribe({
        next: (d) => {
          this.dataset.set(d);
          this.promptInput.set('');
          this.slmOutput.set('');
        },
        error: () => this.error.set('Could not capture the fixture.'),
      });
  }

  protected generate(event: Event): void {
    event.preventDefault();
    this.error.set(null);
    this.generating.set(true);
    this.api
      .generateFixtures(
        this.id,
        {
          coverageGoals: this.coverageGoals().trim() || null,
          edgeCases: this.edgeCases().trim() || null,
          constraints: null,
        },
        this.count(),
      )
      .subscribe({
        next: (d) => {
          this.dataset.set(d);
          this.generating.set(false);
        },
        error: () => {
          this.error.set('Could not generate fixtures — a captured seed is required.');
          this.generating.set(false);
        },
      });
  }

  protected addScorer(event: Event): void {
    event.preventDefault();
    this.error.set(null);
    const isJudge = this.isJudge();
    this.evalApi
      .configureScorer(this.id, {
        kind: this.scorerKind(),
        config: this.scorerConfig().trim() || null,
        judgeModel: isJudge ? this.judgeModel() : null,
      })
      .subscribe({
        next: () => {
          this.scorerConfig.set('');
          this.loadScorers();
        },
        error: () => this.error.set('Could not add the scorer — check the config.'),
      });
  }

  protected onPromptChange(promptId: string): void {
    this.selectedPromptId.set(promptId);
    this.selectedVersionId.set('');
    this.versions.set([]);
    if (!promptId) {
      return;
    }
    this.promptsApi.getPrompt(promptId).subscribe({
      next: (p: Prompt) => this.versions.set(p.versions),
    });
  }

  protected triggerRun(event: Event): void {
    event.preventDefault();
    const versionId = this.selectedVersionId();
    if (!versionId) {
      return;
    }
    this.error.set(null);
    this.running.set(true);
    this.evalApi.triggerRun(this.id, this.selectedPromptId(), versionId).subscribe({
      next: (run) => {
        this.running.set(false);
        this.router.navigate(['/eval-runs', run.id]);
      },
      error: () => {
        this.error.set('Could not run the evaluation.');
        this.running.set(false);
      },
    });
  }
}
