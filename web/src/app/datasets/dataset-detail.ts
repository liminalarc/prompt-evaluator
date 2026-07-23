import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Dataset } from '../dataset';
import { EvalRunSummary, SCORER_KINDS, ScorerConfig, ScorerKind } from '../eval-run';
import { PromptVersion } from '../prompt';
import { ModelCatalogEntry } from '../model';
import { EvalRunsApiService } from '../eval-runs/eval-runs-api.service';
import { ModelsApiService } from '../models/models-api.service';
import { PromptsApiService } from '../prompts/prompts-api.service';
import {
  Breadcrumb,
  Card,
  CardFoot,
  Chip,
  ChipList,
  ConfirmService,
  Crumb,
  Drawer,
  EmptyState,
  ErrorState,
  LoadingState,
  MarkdownEditor,
  PageHeader,
  StatusBadge,
  originBadge,
  runFailureMessage,
  serverError,
} from '../shared';
import { DatasetsApiService } from './datasets-api.service';

type OriginFilter = 'all' | 'Captured' | 'Synthetic';

@Component({
  selector: 'app-dataset-detail',
  imports: [
    FormsModule,
    RouterLink,
    Breadcrumb,
    Card,
    CardFoot,
    Chip,
    ChipList,
    DatePipe,
    DecimalPipe,
    Drawer,
    EmptyState,
    ErrorState,
    LoadingState,
    MarkdownEditor,
    PageHeader,
    StatusBadge,
  ],
  template: `
    <section class="panel panel--wide">
      <app-breadcrumb [items]="crumbs()" />

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      @if (loading()) {
        <app-loading-state label="Loading dataset…" />
      } @else if (dataset(); as d) {
        <app-page-header [heading]="'Dataset: ' + d.name" [subtitle]="d.description ?? ''">
          <button
            actions
            class="sb-btn sb-btn--danger sb-btn--sm"
            type="button"
            data-testid="delete-dataset"
            (click)="deleteDataset(d)"
          >
            Delete dataset
          </button>
        </app-page-header>

        <app-card heading="Test cases">
          <p class="subtitle">
            A dataset is its <strong>test cases</strong> × its <strong>scorers</strong> — every run
            produces a score per test case per scorer.
          </p>
          <div class="field-inline fixtures-filter">
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
            <app-empty-state message="No test cases for this filter." data-testid="no-fixtures" />
          } @else {
            <p class="subtitle">Select a row to view the full test case and edit its label.</p>
            <table class="sb-table" data-testid="fixtures">
              <thead>
                <tr>
                  <th>Origin</th>
                  <th>Label</th>
                  <th>Input</th>
                  <th>Seed</th>
                </tr>
              </thead>
              <tbody>
                @for (f of filteredFixtures(); track f.id) {
                  <tr
                    class="fixture-row"
                    [attr.data-origin]="f.origin"
                    [class.fixture-row--open]="expandedFixtureId() === f.id"
                    (click)="toggleFixture(f.id)"
                    data-testid="fixture-row"
                  >
                    <td>
                      <app-status-badge
                        [variant]="originBadge(f.origin).variant"
                        [label]="originBadge(f.origin).label"
                      />
                    </td>
                    <td>{{ f.label ?? '—' }}</td>
                    <td class="cell-truncate">{{ f.input }}</td>
                    <td>{{ f.seedFixtureId ? 'linked' : '—' }}</td>
                  </tr>
                  @if (expandedFixtureId() === f.id) {
                    <tr class="fixture-detail" data-testid="fixture-detail">
                      <td colspan="4">
                        <dl class="facts">
                          <dt>Input</dt>
                          <dd>{{ f.input }}</dd>
                          <dt>Upstream context</dt>
                          <dd>{{ f.upstreamContext ?? '—' }}</dd>
                          <dt>Expected output</dt>
                          <dd>{{ f.expectedOutput ?? '—' }}</dd>
                        </dl>
                        <form
                          class="form-stack"
                          (submit)="saveFixtureMeta($event, f.id)"
                          (keydown.escape)="cancelEditFixture()"
                        >
                          <div class="sb-field">
                            <label [attr.for]="'flabel-' + f.id">Label</label>
                            <input
                              [attr.id]="'flabel-' + f.id"
                              [ngModel]="editFixtureLabel()"
                              (ngModelChange)="editFixtureLabel.set($event)"
                              [ngModelOptions]="{ standalone: true }"
                              data-testid="edit-fixture-label"
                            />
                          </div>
                          <div class="sb-field">
                            <label [attr.for]="'fdesc-' + f.id">Description</label>
                            <textarea
                              [attr.id]="'fdesc-' + f.id"
                              rows="2"
                              [ngModel]="editFixtureDescription()"
                              (ngModelChange)="editFixtureDescription.set($event)"
                              [ngModelOptions]="{ standalone: true }"
                              data-testid="edit-fixture-description"
                            ></textarea>
                          </div>
                          <div class="form-actions">
                            <button
                              class="sb-btn sb-btn--primary sb-btn--sm"
                              type="submit"
                              data-testid="save-fixture"
                            >
                              Save
                            </button>
                            <button
                              class="sb-btn sb-btn--ghost sb-btn--sm"
                              type="button"
                              data-testid="cancel-edit-fixture"
                              (click)="cancelEditFixture()"
                            >
                              Cancel
                            </button>
                          </div>
                        </form>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          }

          @if (showCapture()) {
            <form
              class="form-stack reveal-form"
              (submit)="capture($event)"
              (keydown.escape)="cancelCapture()"
            >
              <div class="sb-field">
                <label for="fixtureLabel">Label (optional)</label>
                <input
                  id="fixtureLabel"
                  name="fixtureLabel"
                  [ngModel]="fixtureLabel()"
                  (ngModelChange)="fixtureLabel.set($event)"
                  data-testid="fixture-label"
                />
              </div>
              <div class="sb-field">
                <label for="fixtureDescription">Description (optional)</label>
                <textarea
                  id="fixtureDescription"
                  name="fixtureDescription"
                  rows="2"
                  [ngModel]="fixtureDescription()"
                  (ngModelChange)="fixtureDescription.set($event)"
                  data-testid="fixture-description"
                ></textarea>
              </div>
              <div class="sb-field">
                <label for="fixtureOrigin">Origin</label>
                <select
                  id="fixtureOrigin"
                  name="fixtureOrigin"
                  [ngModel]="fixtureOrigin()"
                  (ngModelChange)="fixtureOrigin.set($event)"
                  data-testid="fixture-origin"
                >
                  <option value="Captured">Captured (from real app traffic)</option>
                  <option value="Synthetic">Synthetic (hand-written)</option>
                </select>
              </div>
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
              <div class="sb-field">
                <label for="expectedOutput">Expected output (optional)</label>
                <textarea
                  id="expectedOutput"
                  name="expectedOutput"
                  rows="2"
                  [ngModel]="expectedOutput()"
                  (ngModelChange)="expectedOutput.set($event)"
                  data-testid="expected-output"
                ></textarea>
              </div>
              <div class="form-actions">
                <button class="sb-btn sb-btn--primary" type="submit" data-testid="capture">
                  Add test case
                </button>
                <button
                  class="sb-btn sb-btn--ghost"
                  type="button"
                  data-testid="cancel-capture"
                  (click)="cancelCapture()"
                >
                  Cancel
                </button>
              </div>
            </form>
          }

          @if (showGenerate()) {
            <form
              class="form-stack reveal-form"
              (submit)="generate($event)"
              (keydown.escape)="cancelGenerate()"
            >
              <p class="subtitle">
                Seeded from this dataset's captured test cases; steer with guidance.
              </p>
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
              <div class="form-actions">
                <button
                  class="sb-btn sb-btn--primary"
                  type="submit"
                  data-testid="generate"
                  [disabled]="generating()"
                >
                  {{ generating() ? 'Generating…' : 'Generate' }}
                </button>
                <button
                  class="sb-btn sb-btn--ghost"
                  type="button"
                  data-testid="cancel-generate"
                  (click)="cancelGenerate()"
                >
                  Cancel
                </button>
              </div>
            </form>
          }

          <div foot class="toolbar">
            <button
              class="sb-btn sb-btn--sm sb-btn--secondary"
              type="button"
              data-testid="toggle-capture"
              (click)="showCapture.set(!showCapture())"
            >
              + Add test case
            </button>
            <button
              class="sb-btn sb-btn--sm sb-btn--secondary"
              type="button"
              data-testid="toggle-generate"
              (click)="showGenerate.set(!showGenerate())"
            >
              + Generate synthetic
            </button>
          </div>
        </app-card>

        <app-card heading="Scorers">
          <p class="subtitle">Configured once per dataset; every run scores with this set.</p>
          @if (scorers().length === 0) {
            <app-empty-state message="No scorers configured yet." data-testid="no-scorers" />
          } @else {
            <p class="subtitle">Select a scorer to edit or remove it.</p>
            <table class="sb-table" data-testid="scorers">
              <thead>
                <tr>
                  <th>Kind</th>
                  <th>Config</th>
                  <th>Judge model</th>
                  <th>Weight</th>
                </tr>
              </thead>
              <tbody>
                @for (s of scorers(); track s.id) {
                  <tr
                    class="scorer-row"
                    [attr.data-scorer]="s.kind"
                    [class.scorer-row--open]="expandedScorerId() === s.id"
                    (click)="toggleScorer(s)"
                    data-testid="scorer-row"
                  >
                    <td><app-chip [label]="s.kind" /></td>
                    <td class="config-cell" [attr.title]="s.config">
                      {{ configSummary(s.config) }}
                    </td>
                    <td>
                      @if (s.judgeModel; as m) {
                        <app-chip [label]="m" />
                      } @else {
                        —
                      }
                    </td>
                    <td data-testid="scorer-weight">×{{ s.weight }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }

          <!-- W22 (D1): editing a scorer is a focused task with a big rubric editor → it opens in
               the shared right drawer instead of expanding inline and reflowing the table. -->
          @if (editingScorer(); as s) {
            <app-drawer [open]="true" heading="Edit scorer" (closed)="cancelEditScorer()">
              <form
                class="form-stack"
                data-testid="scorer-detail"
                (submit)="saveScorer($event, s.id)"
                (keydown.escape)="cancelEditScorer()"
              >
                <div class="sb-field">
                  <label [attr.for]="'ekind-' + s.id">Scorer</label>
                  <select
                    [attr.id]="'ekind-' + s.id"
                    [ngModel]="editScorerKind()"
                    (ngModelChange)="editScorerKind.set($event)"
                    [ngModelOptions]="{ standalone: true }"
                    data-testid="edit-scorer-kind"
                  >
                    @for (k of scorerKinds; track k) {
                      <option [value]="k">{{ k }}</option>
                    }
                  </select>
                </div>
                <div class="sb-field">
                  <label [attr.for]="'econfig-' + s.id">{{ editConfigLabel() }}</label>
                  @if (editIsJudge()) {
                    <app-markdown-editor
                      [inputId]="'econfig-' + s.id"
                      [rows]="12"
                      testid="edit-scorer-config"
                      [value]="editScorerConfig()"
                      (valueChange)="editScorerConfig.set($event)"
                    />
                  } @else {
                    <textarea
                      class="config-source"
                      [attr.id]="'econfig-' + s.id"
                      rows="4"
                      [ngModel]="editScorerConfig()"
                      (ngModelChange)="editScorerConfig.set($event)"
                      [ngModelOptions]="{ standalone: true }"
                      data-testid="edit-scorer-config"
                    ></textarea>
                  }
                </div>
                @if (editIsJudge()) {
                  <div class="sb-field">
                    <label [attr.for]="'ejudge-' + s.id">Judge model</label>
                    <select
                      [attr.id]="'ejudge-' + s.id"
                      [ngModel]="editJudgeModel()"
                      (ngModelChange)="editJudgeModel.set($event)"
                      [ngModelOptions]="{ standalone: true }"
                      data-testid="edit-judge-model"
                    >
                      @for (m of judgeModels(); track m.modelId) {
                        <option [value]="m.modelId">
                          {{ m.displayName }}{{ m.available ? '' : ' (unavailable)' }}
                        </option>
                      }
                    </select>
                  </div>
                }
                <div class="sb-field">
                  <label [attr.for]="'eweight-' + s.id">Composite weight</label>
                  <input
                    type="number"
                    min="0.1"
                    step="0.1"
                    [attr.id]="'eweight-' + s.id"
                    [ngModel]="editScorerWeight()"
                    (ngModelChange)="editScorerWeight.set($event)"
                    [ngModelOptions]="{ standalone: true }"
                    data-testid="edit-scorer-weight"
                  />
                  <p class="subtitle">Relative weight in the dataset's overall composite score.</p>
                </div>
                <div class="toolbar">
                  <button
                    class="sb-btn sb-btn--primary sb-btn--sm"
                    type="submit"
                    data-testid="save-scorer"
                    [disabled]="!editConfigValid()"
                  >
                    Save
                  </button>
                  <button
                    class="sb-btn sb-btn--ghost sb-btn--sm"
                    type="button"
                    data-testid="cancel-edit-scorer"
                    (click)="cancelEditScorer()"
                  >
                    Cancel
                  </button>
                  <button
                    class="sb-btn sb-btn--danger sb-btn--sm"
                    type="button"
                    data-testid="remove-scorer"
                    (click)="removeScorer(s.id)"
                  >
                    Remove
                  </button>
                </div>
              </form>
            </app-drawer>
          }

          @if (showAddScorer()) {
            <form
              class="form-stack reveal-form"
              (submit)="addScorer($event)"
              (keydown.escape)="cancelAddScorer()"
            >
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
                <label for="scorerConfig">{{ configLabel() }}</label>
                <!-- The LlmJudge rubric is markdown-bearing → the markdown editor (2.10). Regex /
                     JsonSchema configs are patterns/schemas, not prose, so they stay plain. -->
                @if (isJudge()) {
                  <app-markdown-editor
                    inputId="scorerConfig"
                    name="scorerConfig"
                    [rows]="6"
                    testid="scorer-config"
                    [value]="scorerConfig()"
                    (valueChange)="scorerConfig.set($event)"
                  />
                } @else {
                  <textarea
                    class="config-source"
                    id="scorerConfig"
                    name="scorerConfig"
                    rows="3"
                    [ngModel]="scorerConfig()"
                    (ngModelChange)="scorerConfig.set($event)"
                    data-testid="scorer-config"
                  ></textarea>
                }
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
                    @for (m of judgeModels(); track m.modelId) {
                      <option [value]="m.modelId">
                        {{ m.displayName }}{{ m.available ? '' : ' (unavailable)' }}
                      </option>
                    }
                  </select>
                </div>
              }
              <div class="sb-field">
                <label for="scorerWeight">Composite weight</label>
                <input
                  type="number"
                  min="0.1"
                  step="0.1"
                  id="scorerWeight"
                  name="scorerWeight"
                  [ngModel]="scorerWeight()"
                  (ngModelChange)="scorerWeight.set($event)"
                  data-testid="scorer-weight"
                />
                <p class="subtitle">
                  Relative weight in the dataset's overall composite score (default 1).
                </p>
              </div>
              <div class="form-actions">
                <button
                  class="sb-btn sb-btn--primary"
                  type="submit"
                  data-testid="add-scorer"
                  [disabled]="!configValid()"
                >
                  Add scorer
                </button>
                <button
                  class="sb-btn sb-btn--ghost"
                  type="button"
                  data-testid="cancel-add-scorer"
                  (click)="cancelAddScorer()"
                >
                  Cancel
                </button>
              </div>
            </form>
          }

          <button
            foot
            class="sb-btn sb-btn--sm sb-btn--secondary"
            type="button"
            data-testid="toggle-add-scorer"
            (click)="showAddScorer.set(!showAddScorer())"
          >
            + Add scorer
          </button>
        </app-card>

        <div class="card-grid">
          <app-card heading="Run evaluation">
            <form class="form-stack" (submit)="triggerRun($event)">
              <div class="sb-field">
                <label>Prompt</label>
                <!-- A dataset belongs to one prompt (1.7); the run form is fixed to it — pick a
                     version only. Removes the cross-org prompt leak (B3). -->
                <p class="fixed-value" data-testid="run-prompt">{{ promptName() ?? '—' }}</p>
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
          </app-card>

          <app-card heading="Runs">
            @if (runs().length === 0) {
              <app-empty-state message="No runs yet." data-testid="no-runs" />
            } @else {
              <table class="sb-table" data-testid="runs">
                <thead>
                  <tr>
                    <th>Run</th>
                    <th>Version</th>
                    <th>Model</th>
                    <th>Score</th>
                    <th>Scorers</th>
                    <th>Test cases</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of runs(); track r.id) {
                    <tr>
                      <td>
                        <a [routerLink]="['/eval-runs', r.id]" data-testid="run-link">{{
                          r.createdAt | date: 'medium'
                        }}</a>
                      </td>
                      <td>{{ versionLabelFor(r.promptVersionId) }}</td>
                      <td>
                        @if (modelFor(r.promptVersionId); as m) {
                          <app-chip [label]="m" />
                        } @else {
                          —
                        }
                      </td>
                      <td data-testid="run-score">
                        @if (r.meanScore != null) {
                          <strong>{{ r.meanScore | number: '1.2-2' }}</strong>
                          @if (r.meanScorerKind === 'LlmJudge') {
                            <span class="score-hint"> · judge</span>
                          }
                        } @else {
                          —
                        }
                      </td>
                      <td><app-chip-list [labels]="r.scorerKinds" /></td>
                      <td>{{ r.fixtureCount }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </app-card>
        </div>
      }
    </section>
  `,
  styleUrl: '../prompts/prompts.css',
  styles: [
    `
      .fixtures-filter {
        margin-bottom: var(--sb-space-md);
      }
      .reveal-form {
        margin-top: var(--sb-space-lg);
        padding-top: var(--sb-space-lg);
        border-top: 1px solid var(--sb-border);
      }
      .fixed-value {
        margin: 0;
        font-weight: 600;
      }
      .fixture-row,
      .scorer-row {
        cursor: pointer;
      }
      .fixture-row--open,
      .scorer-row--open {
        background: var(--sb-surface-raised);
      }
      /* W3/W21: Regex/JsonSchema configs are source (patterns/schemas) — monospace + resizable. */
      .config-source {
        width: 100%;
        box-sizing: border-box;
        resize: vertical;
        min-height: 5rem;
        font-family: var(--sb-font-mono);
        font-size: var(--sb-type-small-size);
        line-height: 1.5;
        tab-size: 2;
      }
      /* W20: the CONFIG cell shows a one-line summary (a full rubric would be a wall of text);
         the row expands to reveal + edit the full config. */
      .config-cell {
        color: var(--sb-text-muted);
        max-width: 30rem;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .score-hint {
        color: var(--sb-text-muted);
        font-size: var(--sb-type-small-size);
      }
      .cell-truncate {
        max-width: 28rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        /* The label is the identifier; the raw-input preview is secondary (W19). */
        color: var(--sb-text-muted);
        font-size: var(--sb-type-small-size);
      }
      .facts {
        margin: 0 0 var(--sb-space-md);
      }
      .facts dt {
        font-size: var(--sb-type-small-size);
        color: var(--sb-text-secondary);
        margin-top: var(--sb-space-sm);
      }
      .facts dd {
        margin: 0;
        white-space: pre-wrap;
        word-break: break-word;
      }
    `,
  ],
})
export class DatasetDetail implements OnInit {
  private readonly api = inject(DatasetsApiService);
  private readonly evalApi = inject(EvalRunsApiService);
  private readonly modelsApi = inject(ModelsApiService);
  private readonly promptsApi = inject(PromptsApiService);
  private readonly confirm = inject(ConfirmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly scorerKinds = SCORER_KINDS;

  // Judge-model droplist, sourced from the catalog (1.13) — judge-capable models across providers.
  protected readonly models = signal<ModelCatalogEntry[]>([]);
  protected readonly judgeModels = computed(() =>
    this.models().filter((m) => m.roles.includes('judge')),
  );

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
  protected readonly expectedOutput = signal('');
  // Manual add-fixture metadata: label/description + operator-chosen origin (U7/U8).
  // U18: manual entry is hand-written, so the honest default is Synthetic (was Captured).
  protected readonly fixtureLabel = signal('');
  protected readonly fixtureDescription = signal('');
  protected readonly fixtureOrigin = signal<'Captured' | 'Synthetic'>('Synthetic');
  // Fixture rows expand to a detail panel with an inline metadata editor (U6/U7).
  protected readonly expandedFixtureId = signal<string | null>(null);
  protected readonly editFixtureLabel = signal('');
  protected readonly editFixtureDescription = signal('');
  protected readonly coverageGoals = signal('');
  protected readonly edgeCases = signal('');
  protected readonly count = signal(5);

  protected readonly scorers = signal<ScorerConfig[]>([]);
  protected readonly scorerKind = signal<ScorerKind>('Regex');
  protected readonly scorerConfig = signal('');
  protected readonly judgeModel = signal('');
  protected readonly scorerWeight = signal(1);

  // Clicking a scorer row opens its edit form in the shared right drawer (W22/D1): reconfigure the
  // descriptor or remove the scorer. `expandedScorerId` names the open one; `editingScorer` resolves
  // it to the row so the drawer (rendered outside the @for) can bind to it.
  protected readonly expandedScorerId = signal<string | null>(null);
  protected readonly editingScorer = computed(() =>
    this.scorers().find((s) => s.id === this.expandedScorerId()),
  );
  protected readonly editScorerKind = signal<ScorerKind>('Regex');
  protected readonly editScorerConfig = signal('');
  protected readonly editJudgeModel = signal('');
  protected readonly editScorerWeight = signal(1);

  // The run form is fixed to the dataset's owning prompt (B3): its versions load with the dataset
  // and the operator picks a version only — no free (cross-org) prompt choice.
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

  // Runs table (U14): resolve a run's version id to a readable "vN" + its target model, using the
  // owning prompt's versions already loaded for the run form.
  protected versionLabelFor(versionId: string): string {
    const v = this.versions().find((x) => x.id === versionId);
    return v ? `v${v.versionNumber}` : '—';
  }
  protected modelFor(versionId: string): string | null {
    return this.versions().find((x) => x.id === versionId)?.targetModel ?? null;
  }

  protected readonly isJudge = computed(() => this.scorerKind() === 'LlmJudge');

  // Config is mandatory for Regex/JsonSchema (the pattern/schema) and for LlmJudge (the rubric);
  // other deterministic kinds treat it as optional. Mirrors Domain.ScorerDescriptor (B5).
  protected readonly requiresConfig = computed(() => {
    const k = this.scorerKind();
    return k === 'Regex' || k === 'JsonSchema' || k === 'LlmJudge';
  });
  protected readonly configValid = computed(
    () => !this.requiresConfig() || this.scorerConfig().trim().length > 0,
  );
  protected readonly configLabel = computed(() => {
    if (this.isJudge()) return 'Rubric';
    return this.requiresConfig() ? 'Config (required)' : 'Config (optional)';
  });

  // W20: a scannable one-line summary of a scorer's config for the table cell — a full LlmJudge
  // rubric is a wall of text, so collapse whitespace and truncate. The row expands to the full text.
  protected configSummary(config: string | null): string {
    if (!config) return '—';
    const oneLine = config.replace(/\s+/g, ' ').trim();
    return oneLine.length > 60 ? oneLine.slice(0, 60) + '…' : oneLine;
  }

  // Same validation/labelling for the inline scorer edit form (U9).
  protected readonly editIsJudge = computed(() => this.editScorerKind() === 'LlmJudge');
  protected readonly editRequiresConfig = computed(() => {
    const k = this.editScorerKind();
    return k === 'Regex' || k === 'JsonSchema' || k === 'LlmJudge';
  });
  protected readonly editConfigValid = computed(
    () => !this.editRequiresConfig() || this.editScorerConfig().trim().length > 0,
  );
  protected readonly editConfigLabel = computed(() => {
    if (this.editIsJudge()) return 'Rubric';
    return this.editRequiresConfig() ? 'Config (required)' : 'Config (optional)';
  });

  // A dataset lives with a prompt (1.7) — the trail leads back through its owning prompt workspace.
  protected readonly crumbs = computed<Crumb[]>(() => {
    const d = this.dataset();
    if (!d) return [{ label: 'Dashboard', link: '/' }];
    return [
      { label: 'Dashboard', link: '/' },
      // Return to the workspace's Datasets tab — where this dataset was opened from.
      {
        label: this.promptName() ?? 'Prompt',
        link: ['/prompts', d.promptId],
        queryParams: { tab: 'datasets' },
      },
      { label: d.name },
    ];
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
    this.loadScorers();
    this.loadRuns();
    this.modelsApi.listModels().subscribe({
      next: (m) => {
        this.models.set(m);
        // Default the judge selection to the first judge-capable model, so the LlmJudge scorer
        // form is valid without the user having to open and touch the dropdown.
        const firstJudge = this.judgeModels().find((m) => m.available) ?? this.judgeModels()[0];
        if (!this.judgeModel() && firstJudge) {
          this.judgeModel.set(firstJudge.modelId);
        }
      },
    });
  }

  private load(): void {
    this.api.getDataset(this.id).subscribe({
      next: (d) => {
        this.dataset.set(d);
        this.loading.set(false);
        this.selectedPromptId.set(d.promptId);
        // Load the owning prompt's versions for the run form (fixed to this prompt — B3) and
        // default to the latest so a run is one click away.
        this.promptsApi.getPrompt(d.promptId).subscribe({
          next: (p) => {
            this.promptName.set(p.name);
            this.versions.set(p.versions);
            if (p.versions.length > 0) {
              this.selectedVersionId.set(p.versions[p.versions.length - 1].id);
            }
          },
        });
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
          // The optional expected output maps to Fixture.expectedOutput via downstreamResult (1.2).
          downstreamResult: this.expectedOutput().trim() || null,
          origin: this.fixtureOrigin(),
          label: this.fixtureLabel().trim() || null,
          description: this.fixtureDescription().trim() || null,
        },
      ])
      .subscribe({
        next: (d) => {
          this.dataset.set(d);
          this.promptInput.set('');
          this.slmOutput.set('');
          this.expectedOutput.set('');
          this.fixtureLabel.set('');
          this.fixtureDescription.set('');
          this.fixtureOrigin.set('Synthetic');
          // U17: collapse the reveal form after a successful add — reopen via `+`.
          this.showCapture.set(false);
        },
        error: (err) => this.error.set(serverError(err) ?? 'Could not add the test case.'),
      });
  }

  // Expand/collapse a fixture row; on expand, seed the metadata editor from that fixture.
  protected toggleFixture(fixtureId: string): void {
    if (this.expandedFixtureId() === fixtureId) {
      this.expandedFixtureId.set(null);
      return;
    }
    const f = this.dataset()?.fixtures.find((x) => x.id === fixtureId);
    this.editFixtureLabel.set(f?.label ?? '');
    this.editFixtureDescription.set(f?.description ?? '');
    this.expandedFixtureId.set(fixtureId);
  }

  protected saveFixtureMeta(event: Event, fixtureId: string): void {
    event.preventDefault();
    this.error.set(null);
    this.api
      .editFixture(
        this.id,
        fixtureId,
        this.editFixtureLabel().trim() || null,
        this.editFixtureDescription().trim() || null,
      )
      .subscribe({
        next: (d) => {
          this.dataset.set(d);
          this.expandedFixtureId.set(null);
        },
        error: (err) => this.error.set(serverError(err) ?? 'Could not update the test case.'),
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
          this.error.set('Could not generate test cases — a captured seed is required.');
          this.generating.set(false);
        },
      });
  }

  protected addScorer(event: Event): void {
    event.preventDefault();
    if (!this.configValid()) {
      this.error.set(
        `Scorer '${this.scorerKind()}' requires a ${this.isJudge() ? 'rubric' : 'config'}.`,
      );
      return;
    }
    this.error.set(null);
    const isJudge = this.isJudge();
    this.evalApi
      .configureScorer(this.id, {
        kind: this.scorerKind(),
        config: this.scorerConfig().trim() || null,
        judgeModel: isJudge ? this.judgeModel() : null,
        weight: this.scorerWeight(),
      })
      .subscribe({
        next: () => {
          this.scorerConfig.set('');
          this.scorerWeight.set(1);
          // U17: collapse the reveal form after a successful add — reopen via `+`.
          this.showAddScorer.set(false);
          this.loadScorers();
        },
        error: (err) =>
          this.error.set(serverError(err) ?? 'Could not add the scorer — check the config.'),
      });
  }

  // Expand/collapse a scorer row; on expand, seed the edit form from that scorer (U9).
  protected toggleScorer(scorer: ScorerConfig): void {
    if (this.expandedScorerId() === scorer.id) {
      this.expandedScorerId.set(null);
      return;
    }
    this.editScorerKind.set(scorer.kind as ScorerKind);
    this.editScorerConfig.set(scorer.config ?? '');
    this.editJudgeModel.set(scorer.judgeModel ?? '');
    this.editScorerWeight.set(scorer.weight);
    this.expandedScorerId.set(scorer.id);
  }

  protected saveScorer(event: Event, scorerId: string): void {
    event.preventDefault();
    if (!this.editConfigValid()) {
      this.error.set(
        `Scorer '${this.editScorerKind()}' requires a ${this.editIsJudge() ? 'rubric' : 'config'}.`,
      );
      return;
    }
    this.error.set(null);
    const isJudge = this.editIsJudge();
    this.evalApi
      .reconfigureScorer(this.id, scorerId, {
        kind: this.editScorerKind(),
        config: this.editScorerConfig().trim() || null,
        judgeModel: isJudge ? this.editJudgeModel() : null,
        weight: this.editScorerWeight(),
      })
      .subscribe({
        next: () => {
          this.expandedScorerId.set(null);
          this.loadScorers();
        },
        error: (err) => this.error.set(serverError(err) ?? 'Could not update the scorer.'),
      });
  }

  protected async removeScorer(scorerId: string): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Remove scorer',
      message:
        'Removes this scorer from the dataset. Past runs keep their scores; future runs stop using it.',
      confirmLabel: 'Remove scorer',
    });
    if (!ok) return;
    this.error.set(null);
    this.evalApi.deleteScorer(this.id, scorerId).subscribe({
      next: () => {
        this.expandedScorerId.set(null);
        this.loadScorers();
      },
      error: (err) => this.error.set(serverError(err) ?? 'Could not remove the scorer.'),
    });
  }

  protected async deleteDataset(dataset: Dataset): Promise<void> {
    const fixtures = dataset.fixtures?.length ?? 0;
    const ok = await this.confirm.ask({
      title: 'Delete dataset',
      message:
        `Deletes “${dataset.name}” and its ${fixtures} test case(s), its scorers, ` +
        `and all runs and scores against it. This cannot be undone.`,
      confirmLabel: 'Delete dataset',
    });
    if (!ok) return;
    this.error.set(null);
    this.api.deleteDataset(this.id).subscribe({
      // A deleted dataset has nowhere to go — return to its owning prompt workspace.
      next: () => void this.router.navigate(['/prompts', dataset.promptId]),
      error: () => this.error.set('Could not delete the dataset.'),
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
        // Refresh the runs list so the completed run is present when the operator returns to the
        // dataset (no manual Back/reload needed) — finding 5.
        this.loadRuns();
        this.router.navigate(['/eval-runs', run.id]);
      },
      error: (err) => {
        // R2: any run failure is loud — a timeout / non-JSON gateway 5xx has no structured {error}
        // to show, so fall back to a clear timeout/server-error message rather than a silent no-op.
        this.error.set(runFailureMessage(err));
        this.running.set(false);
      },
    });
  }

  // Cancel handlers (2.11): discard the reveal/expand form's unsaved input and collapse back to the
  // summary row / closed toggle. Consistent across every surface — back out without submitting or
  // losing your place. Expand-to-edit rows re-seed their editor on open, so cancel just closes.
  protected cancelCapture(): void {
    this.showCapture.set(false);
    this.promptInput.set('');
    this.slmOutput.set('');
    this.expectedOutput.set('');
    this.fixtureLabel.set('');
    this.fixtureDescription.set('');
    this.fixtureOrigin.set('Captured');
  }

  protected cancelGenerate(): void {
    this.showGenerate.set(false);
    this.coverageGoals.set('');
    this.edgeCases.set('');
    this.count.set(5);
  }

  protected cancelEditFixture(): void {
    this.expandedFixtureId.set(null);
  }

  protected cancelAddScorer(): void {
    this.showAddScorer.set(false);
    this.scorerConfig.set('');
    this.scorerKind.set('Regex');
    this.scorerWeight.set(1);
  }

  protected cancelEditScorer(): void {
    this.expandedScorerId.set(null);
  }
}
