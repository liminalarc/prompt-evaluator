import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Prompt } from '../prompt';
import { DatasetSummary } from '../dataset';
import { EvalRunSummary } from '../eval-run';
import { ModelCatalogEntry } from '../model';
import { TrendSeries } from '../analytics';
import { ModelsApiService } from '../models/models-api.service';
import { DatasetsApiService } from '../datasets/datasets-api.service';
import { EvalRunsApiService } from '../eval-runs/eval-runs-api.service';
import { AnalyticsApiService } from '../analytics/analytics-api.service';
import { TrendChart } from '../analytics/trend-chart';
import {
  Breadcrumb,
  Card,
  CardFoot,
  Chip,
  ChipList,
  ConfirmService,
  Crumb,
  EmptyState,
  ErrorState,
  LoadingState,
  MarkdownEditor,
  PageHeader,
  runFailureMessage,
} from '../shared';
import { PromptsApiService } from './prompts-api.service';
import { VersionDiff } from './version-diff';
import { validateImportFile } from './import-file';

@Component({
  selector: 'app-prompt-detail',
  imports: [
    FormsModule,
    RouterLink,
    VersionDiff,
    TrendChart,
    Breadcrumb,
    Card,
    CardFoot,
    Chip,
    ChipList,
    DatePipe,
    DecimalPipe,
    EmptyState,
    ErrorState,
    LoadingState,
    MarkdownEditor,
    PageHeader,
  ],
  template: `
    <section class="panel panel--wide">
      <app-breadcrumb [items]="crumbs()" />

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      @if (loading()) {
        <app-loading-state label="Loading prompt…" />
      } @else if (prompt(); as p) {
        <app-page-header [heading]="'Prompt: ' + p.name" [subtitle]="p.description ?? ''">
          <button
            actions
            class="sb-btn sb-btn--danger sb-btn--sm"
            type="button"
            data-testid="delete-prompt"
            (click)="deletePrompt(p)"
          >
            Delete prompt
          </button>
        </app-page-header>

        <app-card heading="Version history">
          <p class="subtitle">
            Each version is immutable — <strong>v1, v2…</strong> are the identity; the label is an
            optional description. Select a row to view its content and edit the label.
          </p>
          @if (p.versions.length === 0) {
            <app-empty-state
              message="No versions yet — add the first with the button below."
              data-testid="no-versions"
            />
          } @else {
            <table class="sb-table" data-testid="versions">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Target model</th>
                  <th>Label</th>
                  <th>Created</th>
                </tr>
              </thead>
              <tbody>
                @for (v of p.versions; track v.id) {
                  <tr
                    class="version-row"
                    [class.version-row--open]="expandedVersionId() === v.id"
                    (click)="toggleVersion(v.id)"
                    data-testid="version-row"
                  >
                    <td>v{{ v.versionNumber }}</td>
                    <td><app-chip [label]="v.targetModel" /></td>
                    <td>{{ v.label ?? '—' }}</td>
                    <td>{{ v.createdAt | date: 'medium' }}</td>
                  </tr>
                  @if (expandedVersionId() === v.id) {
                    <tr class="version-detail" data-testid="version-detail">
                      <td colspan="4">
                        <div class="sb-field">
                          <label>Content (immutable — add a version to change it)</label>
                          <pre class="version-content">{{ v.content }}</pre>
                        </div>
                        <form
                          class="form-stack"
                          (submit)="saveLabel($event, v.id)"
                          (keydown.escape)="cancelEditLabel()"
                        >
                          <div class="sb-field">
                            <label [attr.for]="'label-' + v.id">Label (optional description)</label>
                            <input
                              [attr.id]="'label-' + v.id"
                              name="editLabel"
                              [ngModel]="editLabel()"
                              (ngModelChange)="editLabel.set($event)"
                              [ngModelOptions]="{ standalone: true }"
                              data-testid="edit-label"
                            />
                          </div>
                          <div class="form-actions">
                            <button
                              class="sb-btn sb-btn--primary sb-btn--sm"
                              type="submit"
                              data-testid="save-label"
                            >
                              Save label
                            </button>
                            <button
                              class="sb-btn sb-btn--ghost sb-btn--sm"
                              type="button"
                              data-testid="cancel-edit-label"
                              (click)="cancelEditLabel()"
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

          @if (showAddVersion()) {
            <form
              class="form-stack add-version-form"
              (submit)="addVersion($event)"
              (keydown.escape)="cancelAddVersion()"
            >
              <div class="sb-field">
                <label for="importFile">Import content from a file (optional)</label>
                <input
                  id="importFile"
                  type="file"
                  accept=".txt,.md,.markdown,.text,.prompt,text/*"
                  data-testid="import-version-file"
                  (change)="importVersionFile($event)"
                />
              </div>
              <div class="sb-field">
                <label for="content">Content</label>
                <app-markdown-editor
                  inputId="content"
                  name="content"
                  [rows]="8"
                  [value]="content()"
                  (valueChange)="content.set($event)"
                />
              </div>
              <div class="sb-field">
                <label for="targetModel">Target model</label>
                <select
                  id="targetModel"
                  name="targetModel"
                  [ngModel]="targetModel()"
                  (ngModelChange)="targetModel.set($event)"
                  [ngModelOptions]="{ standalone: true }"
                  data-testid="target-model"
                >
                  @for (m of subjectModels(); track m.modelId) {
                    <option [value]="m.modelId">
                      {{ m.displayName }}{{ m.available ? '' : ' (unavailable)' }}
                    </option>
                  }
                </select>
                @if (targetModelChanged()) {
                  <p class="model-warn" data-testid="model-change-warning">
                    ⚠ This changes the subject model from the last version ({{
                      modelDisplay(latestVersionModel())
                    }}). Holding the model constant keeps a version-over-version comparison about
                    the <em>prompt</em>, not a model swap — change it only on purpose.
                  </p>
                }
              </div>
              <div class="sb-field">
                <label for="label">Label (optional description)</label>
                <input
                  id="label"
                  name="label"
                  [ngModel]="label()"
                  (ngModelChange)="label.set($event)"
                />
              </div>
              <div class="form-actions">
                <button class="sb-btn sb-btn--primary" type="submit" data-testid="add-version">
                  Add version
                </button>
                <button
                  class="sb-btn sb-btn--ghost"
                  type="button"
                  data-testid="cancel-add-version"
                  (click)="cancelAddVersion()"
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
            data-testid="toggle-add-version"
            (click)="toggleAddVersion()"
          >
            + Add version
          </button>
        </app-card>

        @if (p.versions.length >= 2) {
          <app-card heading="Compare versions">
            <div class="compare">
              <label
                >From
                <select
                  [ngModel]="fromNumber()"
                  (ngModelChange)="fromNumber.set(+$event)"
                  data-testid="from"
                >
                  @for (v of p.versions; track v.id) {
                    <option [value]="v.versionNumber">v{{ v.versionNumber }}</option>
                  }
                </select>
              </label>
              <label
                >To
                <select
                  [ngModel]="toNumber()"
                  (ngModelChange)="toNumber.set(+$event)"
                  data-testid="to"
                >
                  @for (v of p.versions; track v.id) {
                    <option [value]="v.versionNumber">v{{ v.versionNumber }}</option>
                  }
                </select>
              </label>
            </div>
            <app-version-diff [before]="fromContent()" [after]="toContent()" />
          </app-card>
        }

        @if (p.versions.length > 0 && datasets()?.length) {
          <app-card heading="Run a version">
            <p class="subtitle">Score a version against one of this prompt's datasets.</p>
            @if (showRun()) {
              <form
                class="form-stack add-version-form"
                (submit)="triggerRun($event)"
                (keydown.escape)="cancelRun()"
              >
                <div class="sb-field">
                  <label for="runVersion">Version</label>
                  <select
                    id="runVersion"
                    name="runVersion"
                    [ngModel]="runVersionId()"
                    (ngModelChange)="runVersionId.set($event)"
                    data-testid="run-version"
                  >
                    <option value="">Select a version…</option>
                    @for (v of p.versions; track v.id) {
                      <option [value]="v.id">v{{ v.versionNumber }} · {{ v.targetModel }}</option>
                    }
                  </select>
                </div>
                <div class="sb-field">
                  <label for="runDataset">Dataset</label>
                  <select
                    id="runDataset"
                    name="runDataset"
                    [ngModel]="runDatasetId()"
                    (ngModelChange)="onRunDatasetChange($event)"
                    data-testid="run-dataset"
                  >
                    <option value="">Select a dataset…</option>
                    @for (d of datasets(); track d.id) {
                      <option [value]="d.id">{{ d.name }}</option>
                    }
                  </select>
                </div>
                <div class="form-actions">
                  <button
                    class="sb-btn sb-btn--primary"
                    type="submit"
                    data-testid="run"
                    [disabled]="running() || !runVersionId() || !runDatasetId()"
                  >
                    {{ running() ? 'Running…' : 'Run evaluation' }}
                  </button>
                  <button
                    class="sb-btn sb-btn--ghost"
                    type="button"
                    data-testid="cancel-run"
                    (click)="cancelRun()"
                  >
                    Cancel
                  </button>
                </div>
              </form>

              @if (recentRuns().length > 0) {
                <table class="sb-table" data-testid="recent-runs">
                  <thead>
                    <tr>
                      <th>Run</th>
                      <th>Score</th>
                      <th>Scorers</th>
                      <th>Test cases</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (r of recentRuns(); track r.id) {
                      <tr>
                        <td>
                          <a [routerLink]="['/eval-runs', r.id]" data-testid="recent-run-link">{{
                            r.createdAt | date: 'medium'
                          }}</a>
                        </td>
                        <td>
                          {{ r.meanScore != null ? (r.meanScore | number: '1.2-2') : '—' }}
                        </td>
                        <td><app-chip-list [labels]="r.scorerKinds" /></td>
                        <td>{{ r.fixtureCount }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
            }
            <button
              foot
              class="sb-btn sb-btn--sm sb-btn--secondary"
              type="button"
              data-testid="toggle-run"
              (click)="showRun.set(!showRun())"
            >
              + Run a version
            </button>
          </app-card>
        }

        <div class="card-grid">
          <app-card heading="Datasets">
            <p class="subtitle">
              This prompt's test sets — its test cases and the runs scored against them.
            </p>
            @if (datasets(); as ds) {
              @if (ds.length === 0) {
                <app-empty-state
                  message="No datasets yet — create one with the button below."
                  data-testid="no-datasets"
                />
              } @else {
                <table class="sb-table" data-testid="datasets">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Test cases</th>
                      <th>Captured</th>
                      <th>Synthetic</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (d of ds; track d.id) {
                      <tr>
                        <td>
                          <a [routerLink]="['/datasets', d.id]">{{ d.name }}</a>
                        </td>
                        <td>{{ d.fixtureCount }}</td>
                        <td>{{ d.capturedCount }}</td>
                        <td>{{ d.syntheticCount }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
            }

            @if (showCreateDataset()) {
              <form
                class="form-stack create-dataset-form"
                (submit)="createDataset($event)"
                (keydown.escape)="cancelCreateDataset()"
              >
                <div class="sb-field">
                  <label for="datasetName">New dataset name</label>
                  <input
                    id="datasetName"
                    name="datasetName"
                    [ngModel]="datasetName()"
                    (ngModelChange)="datasetName.set($event)"
                  />
                </div>
                <div class="sb-field">
                  <label for="datasetDescription">Description (optional)</label>
                  <textarea
                    id="datasetDescription"
                    name="datasetDescription"
                    rows="2"
                    [ngModel]="datasetDescription()"
                    (ngModelChange)="datasetDescription.set($event)"
                    data-testid="dataset-description"
                  ></textarea>
                </div>
                <div class="form-actions">
                  <button class="sb-btn sb-btn--primary" type="submit" data-testid="create-dataset">
                    Add dataset
                  </button>
                  <button
                    class="sb-btn sb-btn--ghost"
                    type="button"
                    data-testid="cancel-create-dataset"
                    (click)="cancelCreateDataset()"
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
              data-testid="toggle-create-dataset"
              (click)="showCreateDataset.set(!showCreateDataset())"
            >
              + New dataset
            </button>
          </app-card>

          <app-card heading="Analytics">
            @if (datasets()?.length) {
              <div class="sb-field">
                <label for="analyticsDataset">Dataset</label>
                <select
                  id="analyticsDataset"
                  [ngModel]="selectedDatasetId()"
                  (ngModelChange)="selectDataset($event)"
                  data-testid="analytics-dataset"
                >
                  <option value="">Select a dataset…</option>
                  @for (d of datasets(); track d.id) {
                    <option [value]="d.id">{{ d.name }}</option>
                  }
                </select>
              </div>
              @if (selectedDatasetId()) {
                <app-trend-chart [series]="trends()" />
              }
            } @else {
              <app-empty-state
                message="Add a dataset and run this prompt to see analytics."
                data-testid="no-analytics"
              />
            }
          </app-card>
        </div>
      }
    </section>
  `,
  styleUrl: './prompts.css',
  styles: [
    `
      .compare {
        display: flex;
        gap: var(--sb-space-lg);
        font-size: var(--sb-type-small-size);
        color: var(--sb-text-secondary);
        margin-bottom: var(--sb-space-md);
      }
      .compare label {
        display: flex;
        align-items: center;
        gap: var(--sb-space-sm);
      }
      .add-version-form,
      .create-dataset-form {
        margin-top: var(--sb-space-lg);
        padding-top: var(--sb-space-lg);
        border-top: 1px solid var(--sb-border);
      }
      .version-row {
        cursor: pointer;
      }
      .version-row--open {
        background: var(--sb-surface-raised);
      }
      .version-content {
        white-space: pre-wrap;
        word-break: break-word;
        margin: 0;
        padding: var(--sb-space-md);
        background: var(--sb-surface-raised);
        border-radius: var(--sb-radius-sm);
        font-size: var(--sb-type-small-size);
      }
      .model-warn {
        margin: var(--sb-space-xs) 0 0;
        font-size: var(--sb-type-small-size);
        color: var(--sb-warning-text, var(--sb-text-secondary));
      }
    `,
  ],
})
export class PromptDetail implements OnInit {
  private readonly api = inject(PromptsApiService);
  private readonly datasetsApi = inject(DatasetsApiService);
  private readonly evalApi = inject(EvalRunsApiService);
  private readonly modelsApi = inject(ModelsApiService);
  private readonly analyticsApi = inject(AnalyticsApiService);
  private readonly confirm = inject(ConfirmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly prompt = signal<Prompt | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  protected readonly crumbs = computed<Crumb[]>(() => [
    { label: 'Dashboard', link: '/' },
    { label: 'Prompts', link: '/prompts' },
    { label: this.prompt()?.name ?? 'Prompt' },
  ]);

  protected readonly fromNumber = signal(1);
  protected readonly toNumber = signal(1);

  protected readonly content = signal('');
  protected readonly targetModel = signal('claude-sonnet-5');
  protected readonly label = signal('');

  // Target-model droplist, sourced from the catalog (1.13) — subject-capable models only, no typing.
  protected readonly models = signal<ModelCatalogEntry[]>([]);
  protected readonly subjectModels = computed(() =>
    this.models().filter((m) => m.roles.includes('subject')),
  );

  // R5 — hold the subject model constant across versions. The latest version's model is the default
  // for a new version (holding it is the default, not a thing to remember); a change is warned so a
  // silent model drift can't confound a prompt-vs-prompt comparison.
  protected readonly latestVersionModel = computed<string | null>(() => {
    const versions = this.prompt()?.versions ?? [];
    return versions.length ? versions[versions.length - 1].targetModel : null;
  });
  protected readonly targetModelChanged = computed(() => {
    const latest = this.latestVersionModel();
    return !!latest && this.targetModel() !== latest;
  });
  protected modelDisplay(modelId: string | null): string {
    if (!modelId) return '';
    return this.models().find((m) => m.modelId === modelId)?.displayName ?? modelId;
  }

  // Progressive disclosure: the version history + datasets stay visible; the create forms reveal.
  protected readonly showAddVersion = signal(false);
  protected readonly showCreateDataset = signal(false);

  // Version history is a collapsible list (U2): a row expands to show its (immutable) content and
  // an editable label (U3). Only one row is open at a time.
  protected readonly expandedVersionId = signal<string | null>(null);
  protected readonly editLabel = signal('');

  // Datasets + analytics — this prompt's, shown together with it (1.7).
  protected readonly datasets = signal<DatasetSummary[] | null>(null);
  protected readonly datasetName = signal('');
  protected readonly datasetDescription = signal('');

  // Run-from-workspace (U13): pick a version + one of this prompt's datasets, trigger a run and see
  // recent runs — no hop to the dataset page. Revealed behind a toggle (progressive disclosure).
  protected readonly showRun = signal(false);
  protected readonly runVersionId = signal('');
  protected readonly runDatasetId = signal('');
  protected readonly running = signal(false);
  protected readonly recentRuns = signal<EvalRunSummary[]>([]);
  protected readonly selectedDatasetId = signal('');
  protected readonly trends = signal<TrendSeries[] | null>(null);

  private id = '';

  protected readonly fromContent = computed(() => this.contentOf(this.fromNumber()));
  protected readonly toContent = computed(() => this.contentOf(this.toNumber()));

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
    this.loadDatasets();
    this.modelsApi.listModels().subscribe({ next: (m) => this.models.set(m) });
  }

  private load(): void {
    this.api.getPrompt(this.id).subscribe({
      next: (p) => {
        this.prompt.set(p);
        this.loading.set(false);
        if (p.versions.length >= 2) {
          this.fromNumber.set(p.versions[p.versions.length - 2].versionNumber);
          this.toNumber.set(p.versions[p.versions.length - 1].versionNumber);
        }
      },
      error: () => {
        this.error.set('Could not load the prompt.');
        this.loading.set(false);
      },
    });
  }

  private loadDatasets(): void {
    this.datasetsApi.listDatasetsByPrompt(this.id).subscribe({
      next: (list) => this.datasets.set(list),
      error: () => this.error.set('Could not load the prompt’s datasets.'),
    });
  }

  protected createDataset(event: Event): void {
    event.preventDefault();
    const name = this.datasetName().trim();
    if (!name) return;
    this.error.set(null);
    this.datasetsApi
      .createDataset(this.id, name, this.datasetDescription().trim() || null)
      .subscribe({
        next: () => {
          this.datasetName.set('');
          this.datasetDescription.set('');
          this.loadDatasets();
        },
        error: () => this.error.set('Could not create the dataset.'),
      });
  }

  // U13: when a dataset is chosen in the Run card, show its recent runs inline.
  protected onRunDatasetChange(datasetId: string): void {
    this.runDatasetId.set(datasetId);
    this.recentRuns.set([]);
    if (!datasetId) return;
    this.evalApi.listRuns(datasetId).subscribe({ next: (runs) => this.recentRuns.set(runs) });
  }

  protected triggerRun(event: Event): void {
    event.preventDefault();
    const versionId = this.runVersionId();
    const datasetId = this.runDatasetId();
    if (!versionId || !datasetId) return;
    this.error.set(null);
    this.running.set(true);
    this.evalApi.triggerRun(datasetId, this.id, versionId).subscribe({
      next: (run) => {
        this.running.set(false);
        void this.router.navigate(['/eval-runs', run.id]);
      },
      error: (err) => {
        // R2: loud on any failure — a timeout / non-JSON 5xx yields a clear message, never a no-op.
        this.error.set(runFailureMessage(err));
        this.running.set(false);
      },
    });
  }

  protected selectDataset(datasetId: string): void {
    this.selectedDatasetId.set(datasetId);
    this.trends.set(null);
    if (!datasetId) return;
    this.analyticsApi.getTrends(this.id, datasetId).subscribe({
      next: (series) => this.trends.set(series),
      error: () => this.error.set('Could not load analytics for the dataset.'),
    });
  }

  private contentOf(versionNumber: number): string {
    return this.prompt()?.versions.find((v) => v.versionNumber === versionNumber)?.content ?? '';
  }

  protected async deletePrompt(p: Prompt): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Delete prompt',
      message:
        `Deletes “${p.name}” and its ${p.versions.length} version(s), along with its ` +
        `datasets and all their runs and scores. This cannot be undone.`,
      confirmLabel: 'Delete prompt',
    });
    if (!ok) return;
    this.error.set(null);
    this.api.deletePrompt(this.id).subscribe({
      next: () => void this.router.navigate(['/prompts']),
      error: () => this.error.set('Could not delete the prompt.'),
    });
  }

  /**
   * Single-file import (1.6): validate the picked file, then read its text into the existing
   * `content` signal. The unchanged add-version POST does the actual copy-in — no new API.
   */
  protected importVersionFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const check = validateImportFile(file);
    if (!check.ok) {
      this.error.set(check.message ?? 'That file could not be imported.');
      input.value = ''; // let the same file be re-picked after fixing it
      return;
    }

    this.error.set(null);
    const reader = new FileReader();
    reader.onload = () => this.content.set(String(reader.result ?? ''));
    reader.onerror = () => this.error.set('Could not read that file.');
    reader.readAsText(file);
    input.value = '';
  }

  // Expand/collapse a version row; on expand, seed the label editor with that version's label.
  protected toggleVersion(versionId: string): void {
    if (this.expandedVersionId() === versionId) {
      this.expandedVersionId.set(null);
      return;
    }
    const version = this.prompt()?.versions.find((v) => v.id === versionId);
    this.editLabel.set(version?.label ?? '');
    this.expandedVersionId.set(versionId);
  }

  protected saveLabel(event: Event, versionId: string): void {
    event.preventDefault();
    this.error.set(null);
    this.api.editVersionLabel(this.id, versionId, this.editLabel().trim() || null).subscribe({
      next: () => {
        this.expandedVersionId.set(null);
        this.load();
      },
      error: () => this.error.set('Could not update the version label.'),
    });
  }

  // Reveal the add-version form; on open, seed the draft content from the latest version so a new
  // version is edited from the previous one rather than pasted from scratch (U11). Content stays
  // immutable-by-add — this only pre-fills the editable draft.
  protected toggleAddVersion(): void {
    const opening = !this.showAddVersion();
    this.showAddVersion.set(opening);
    if (!opening) return;
    const versions = this.prompt()?.versions ?? [];
    const latest = versions[versions.length - 1];
    if (latest && !this.content().trim()) {
      this.content.set(latest.content);
    }
    // R5: default the Target model to the latest version's model — holding the subject model
    // constant is the default, so a version-over-version comparison isn't confounded by a model swap.
    if (latest) {
      this.targetModel.set(latest.targetModel);
    }
  }

  protected addVersion(event: Event): void {
    event.preventDefault();
    const content = this.content().trim();
    const targetModel = this.targetModel().trim();
    if (!content || !targetModel) {
      return;
    }
    this.error.set(null);
    this.api
      .addVersion(this.id, {
        content,
        targetModel,
        label: this.label().trim() || null,
        sourceApp: null,
      })
      .subscribe({
        next: () => {
          this.content.set('');
          this.label.set('');
          this.load();
        },
        error: () => this.error.set('Could not add the version.'),
      });
  }

  // Cancel handlers (2.11): discard the reveal/expand form's unsaved input and collapse back to the
  // prior state (summary row / closed toggle). Consistent across every surface — back out without
  // submitting or losing your place. The expand-to-edit rows re-seed on open, so cancel just closes.
  protected cancelAddVersion(): void {
    this.showAddVersion.set(false);
    this.content.set('');
    this.label.set('');
    // Discard any model change back to the held default (R5).
    const latest = this.latestVersionModel();
    if (latest) this.targetModel.set(latest);
  }

  protected cancelEditLabel(): void {
    this.expandedVersionId.set(null);
  }

  protected cancelRun(): void {
    this.showRun.set(false);
    this.runVersionId.set('');
    this.runDatasetId.set('');
    this.recentRuns.set([]);
  }

  protected cancelCreateDataset(): void {
    this.showCreateDataset.set(false);
    this.datasetName.set('');
    this.datasetDescription.set('');
  }
}
