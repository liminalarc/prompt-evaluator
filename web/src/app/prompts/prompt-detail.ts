import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';
import { BackportArtifact, Prompt, PromptVersionStatus } from '../prompt';
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
  Drawer,
  EmptyState,
  ErrorState,
  LoadingState,
  MarkdownEditor,
  PageHeader,
  StatusBadge,
  runFailureMessage,
  versionStatusBadges,
  type BadgeSpec,
} from '../shared';
import { PromptsApiService } from './prompts-api.service';
import { CompareDrawer } from '../analytics/compare-drawer';
import { validateImportFile } from './import-file';

type WorkspaceTab = 'versions' | 'datasets' | 'analytics' | 'runs';

@Component({
  selector: 'app-prompt-detail',
  imports: [
    FormsModule,
    RouterLink,
    CompareDrawer,
    TrendChart,
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
        <app-loading-state label="Loading prompt…" />
      } @else if (prompt(); as p) {
        <app-page-header [heading]="'Prompt: ' + p.name" [subtitle]="p.description ?? ''">
          @if (p.versions.length > 0 && datasets()?.length) {
            <button
              actions
              class="sb-btn sb-btn--sm sb-btn--primary"
              type="button"
              data-testid="header-run"
              (click)="openRun()"
            >
              Run a version
            </button>
          }
          @if (p.versions.length >= 2) {
            <button
              actions
              class="sb-btn sb-btn--sm sb-btn--secondary"
              type="button"
              data-testid="open-compare"
              (click)="showCompare.set(true)"
            >
              Compare
            </button>
          }
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

        <!-- D2: the workspace is a tabbed hub (Versions · Datasets · Analytics · Runs). Every panel
             stays in the DOM ([hidden] toggles visibility) so a tab switch is instant and deep. -->
        <nav class="ws-tabs" role="tablist">
          @for (t of tabs; track t.id) {
            <button
              type="button"
              role="tab"
              class="ws-tabs__tab"
              [class.ws-tabs__tab--active]="tab() === t.id"
              [attr.data-testid]="'tab-' + t.id"
              (click)="selectTab(t.id)"
            >
              {{ t.label }}
            </button>
          }
        </nav>

        <div class="tab-panel" [hidden]="tab() !== 'versions'">
          @if (p.versions.length > 0) {
            <!-- 1.16 Deployment summary: which version the source app runs, and whether a better one
                 is waiting to be backported. Mirrors the per-row badges, surfaced up-front. -->
            <app-card heading="Deployment" data-testid="deployment-summary">
              @if (currentVersionNumber(); as cur) {
                <p class="deploy-line">
                  <span class="muted">Current in source:</span>
                  <strong data-testid="deploy-current">v{{ cur }}</strong>
                  @if (p.currentVersionSha) {
                    <code class="deploy-sha">{{ p.currentVersionSha }}</code>
                  }
                </p>
              } @else {
                <p class="muted" data-testid="deploy-none">
                  No version is marked as current in source yet — set one from a version’s row
                  below.
                </p>
              }
              @if (backportTarget(); as target) {
                <div class="deploy-eligible" data-testid="deploy-eligible">
                  <app-status-badge variant="success" label="Backport target" />
                  <span
                    >v{{ target.versionNumber }} is the highest-scoring version above Current — ship
                    it, then mark it backported.</span
                  >
                  <button
                    type="button"
                    class="sb-btn sb-btn--sm sb-btn--secondary"
                    data-testid="prepare-backport"
                    [disabled]="backportLoading()"
                    (click)="prepareBackport()"
                  >
                    Prepare backport
                  </button>
                  <button
                    type="button"
                    class="sb-btn sb-btn--sm sb-btn--primary"
                    data-testid="mark-backported"
                    [disabled]="settingCurrentId() === target.id"
                    (click)="setCurrent(target.id)"
                  >
                    Mark backported → v{{ target.versionNumber }}
                  </button>
                </div>
              }
            </app-card>
          }

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
                    <th>Status</th>
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
                      <td data-testid="version-status">
                        <div class="version-status-cell">
                          @for (b of statusBadgesFor(v.id); track b.label) {
                            <app-status-badge [variant]="b.variant" [label]="b.label" />
                          }
                        </div>
                      </td>
                      <td><app-chip [label]="v.targetModel" /></td>
                      <td>{{ v.label ?? '—' }}</td>
                      <td>{{ v.createdAt | date: 'medium' }}</td>
                    </tr>
                    @if (expandedVersionId() === v.id) {
                      <tr class="version-detail" data-testid="version-detail">
                        <td colspan="5">
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
                              <label [attr.for]="'label-' + v.id"
                                >Label (optional description)</label
                              >
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

                          <!-- 1.16: the "Current in source" marker. Set-current and mark-as-backported
                               are the same action — labelled contextually. -->
                          <div class="version-deploy">
                            @if (versionStatus()?.currentVersionId === v.id) {
                              <span class="muted" data-testid="version-is-current">
                                This is the version your source app runs (Current in source).
                              </span>
                            } @else {
                              <button
                                type="button"
                                class="sb-btn sb-btn--sm sb-btn--secondary"
                                data-testid="set-current"
                                [disabled]="settingCurrentId() === v.id"
                                (click)="setCurrent(v.id)"
                              >
                                Set as current in source
                              </button>
                            }
                          </div>
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
                    [rows]="18"
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
        </div>

        <div class="tab-panel" [hidden]="tab() !== 'runs'">
          @if (p.versions.length > 0 && datasets()?.length) {
            <app-card heading="Runs">
              <p class="subtitle">Every run of this prompt across its datasets, newest first.</p>
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
              }

              @if (runsLoading()) {
                <app-loading-state label="Loading runs…" />
              } @else if (promptRuns().length === 0) {
                <app-empty-state
                  message="No runs yet — run a version to score it."
                  data-testid="no-prompt-runs"
                />
              } @else {
                <table class="sb-table" data-testid="prompt-runs">
                  <thead>
                    <tr>
                      <th>Run</th>
                      <th>Version</th>
                      <th>Dataset</th>
                      <th>Score</th>
                      <th>Scorers</th>
                      <th>Test cases</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (r of promptRuns(); track r.id) {
                      <tr>
                        <td>
                          <a [routerLink]="['/eval-runs', r.id]" data-testid="prompt-run-link">{{
                            r.createdAt | date: 'medium'
                          }}</a>
                        </td>
                        <td>
                          {{
                            versionNumberOf(r.promptVersionId) != null
                              ? 'v' + versionNumberOf(r.promptVersionId)
                              : '—'
                          }}
                        </td>
                        <td>{{ r.datasetName }}</td>
                        <td>{{ r.meanScore != null ? (r.meanScore | number: '1.2-2') : '—' }}</td>
                        <td><app-chip-list [labels]="r.scorerKinds" /></td>
                        <td>{{ r.fixtureCount }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
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
          } @else {
            <app-empty-state
              message="Add a dataset (Datasets tab), then run a version to score it."
              data-testid="no-run-target"
            />
          }
        </div>

        <div class="tab-panel" [hidden]="tab() !== 'datasets'">
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
        </div>

        <div class="tab-panel" [hidden]="tab() !== 'analytics'">
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

        @if (showCompare()) {
          <app-compare-drawer
            [open]="true"
            [promptId]="p.id"
            [versions]="p.versions"
            [datasetId]="compareDatasetId()"
            (closed)="showCompare.set(false)"
          />
        }

        <!-- 1.20 Backport assistance: the ready-to-apply artifact for the single backport target.
             LitmusAI signals only — copy the exact prompt or download the markdown; apply it in the
             source app's own process. -->
        <app-drawer
          [open]="showBackport()"
          heading="Prepare backport"
          (closed)="showBackport.set(false)"
        >
          @if (backportLoading()) {
            <app-loading-state message="Assembling the backport artifact…" />
          } @else if (backportError()) {
            <app-error-state [message]="backportError()!" />
          } @else if (backportArtifact(); as art) {
            <div class="backport" data-testid="backport-drawer">
              <p class="backport__head">
                Ship <strong>v{{ art.currentVersionNumber }}</strong> →
                <strong>v{{ art.targetVersionNumber }}</strong> ·
                <app-chip [label]="art.targetModel" />
              </p>
              <p class="subtitle">
                LitmusAI produces the artifact — apply it in the source app, then
                <strong>Mark backported</strong>. Nothing is written to a source repo.
              </p>

              <div class="backport__actions">
                <button
                  type="button"
                  class="sb-btn sb-btn--sm sb-btn--primary"
                  data-testid="copy-exact-prompt"
                  (click)="copyExactPrompt(art)"
                >
                  {{ copied() ? 'Copied ✓' : 'Copy exact prompt' }}
                </button>
                <button
                  type="button"
                  class="sb-btn sb-btn--sm sb-btn--secondary"
                  data-testid="download-markdown"
                  (click)="downloadMarkdown(art)"
                >
                  Download markdown
                </button>
              </div>

              <div class="sb-field">
                <label>Artifact preview ({{ art.fileName }})</label>
                <pre class="backport__preview" data-testid="backport-markdown">{{
                  art.markdown
                }}</pre>
              </div>
            </div>
          }
        </app-drawer>
      }
    </section>
  `,
  styleUrl: './prompts.css',
  styles: [
    `
      .ws-tabs {
        display: flex;
        gap: var(--sb-space-xs);
        border-bottom: 1px solid var(--sb-border);
        margin-bottom: var(--sb-space-lg);
      }
      .ws-tabs__tab {
        border: none;
        background: transparent;
        padding: var(--sb-space-sm) var(--sb-space-md);
        cursor: pointer;
        color: var(--sb-text-muted);
        border-bottom: 2px solid transparent;
        font-size: var(--sb-type-body-size);
      }
      .ws-tabs__tab--active {
        color: var(--sb-text);
        border-bottom-color: var(--sb-primary);
        font-weight: 600;
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
        /* W4: a long version body must not dominate the page — cap it and scroll internally. */
        max-height: 22rem;
        overflow: auto;
      }
      .model-warn {
        margin: var(--sb-space-xs) 0 0;
        font-size: var(--sb-type-small-size);
        color: var(--sb-warning-text, var(--sb-text-secondary));
      }
      /* 1.16 version-status badges + deployment marker. */
      .muted {
        color: var(--sb-text-secondary);
        font-size: var(--sb-type-small-size);
      }
      .version-status-cell {
        display: flex;
        flex-wrap: wrap;
        gap: 4px;
      }
      .version-deploy {
        margin-top: var(--sb-space-md);
        padding-top: var(--sb-space-sm);
        border-top: 1px solid var(--sb-border);
      }
      .deploy-line {
        display: flex;
        align-items: center;
        gap: var(--sb-space-sm);
        margin: 0 0 var(--sb-space-xs);
      }
      .deploy-sha {
        font-family: var(--sb-font-mono, monospace);
        font-size: var(--sb-type-caption-size);
        color: var(--sb-text-secondary);
      }
      .deploy-eligible {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--sb-space-sm);
        margin-top: var(--sb-space-xs);
      }
      .backport__head {
        display: flex;
        align-items: center;
        gap: var(--sb-space-sm);
        margin: 0 0 var(--sb-space-xs);
      }
      .backport__actions {
        display: flex;
        flex-wrap: wrap;
        gap: var(--sb-space-sm);
        margin: var(--sb-space-md) 0 var(--sb-space-lg);
      }
      .backport__preview {
        white-space: pre-wrap;
        overflow-x: auto;
        max-height: 24rem;
        overflow-y: auto;
        padding: var(--sb-space-md);
        border-radius: var(--sb-radius-md);
        background: var(--sb-surface-variant);
        font-family: var(--sb-font-mono, monospace);
        font-size: var(--sb-type-small-size);
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

  // D2: the workspace hub tabs. Content-compare moved into the shared CompareDrawer (W7).
  protected readonly tabs: { id: WorkspaceTab; label: string }[] = [
    { id: 'versions', label: 'Versions' },
    { id: 'datasets', label: 'Datasets' },
    { id: 'analytics', label: 'Analytics' },
    { id: 'runs', label: 'Runs' },
  ];
  protected readonly tab = signal<WorkspaceTab>('versions');
  protected readonly showCompare = signal(false);
  // The dataset context the Compare drawer's Scores/Rationale tabs use — the one picked in the
  // Analytics tab, else this prompt's first dataset (Content compare needs none).
  protected readonly compareDatasetId = computed(
    () => this.selectedDatasetId() || this.datasets()?.[0]?.id || null,
  );

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

  // Version status & backport lifecycle (1.16): Current-in-source pointer + derived per-version
  // badges (Current / Backport-eligible / Regressed), loaded alongside the prompt.
  protected readonly versionStatus = signal<PromptVersionStatus | null>(null);
  protected readonly settingCurrentId = signal<string | null>(null);

  /** The badges to render for one version row (0+). */
  protected statusBadgesFor(versionId: string): BadgeSpec[] {
    const s = this.versionStatus()?.versions.find((v) => v.versionId === versionId);
    return s ? versionStatusBadges(s) : [];
  }

  /** The version number currently marked "Current in source", or null. */
  protected readonly currentVersionNumber = computed<number | null>(() => {
    const currentId = this.versionStatus()?.currentVersionId ?? null;
    if (!currentId) return null;
    return this.prompt()?.versions.find((v) => v.id === currentId)?.versionNumber ?? null;
  });

  /** The single recommended backport target (the highest-scoring version above Current). */
  protected readonly backportTarget = computed(() => {
    const target = this.versionStatus()?.versions.find((v) => v.isBackportTarget);
    if (!target) return null;
    return { id: target.versionId, versionNumber: target.versionNumber };
  });

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
  // All of this prompt's runs across its datasets — the Runs tab lists these (loaded with the
  // workspace, independent of the run form). Each run carries its dataset name for the table.
  protected readonly promptRuns = signal<(EvalRunSummary & { datasetName: string })[]>([]);
  protected readonly runsLoading = signal(false);
  protected readonly selectedDatasetId = signal('');
  protected readonly trends = signal<TrendSeries[] | null>(null);

  private id = '';

  // Elevate Run (W11): the header action jumps to the Runs tab and opens the compact run form.
  protected openRun(): void {
    this.selectTab('runs');
    this.showRun.set(true);
  }

  // Sync the active tab to a `?tab=` query param (replaceUrl — no history spam). This makes the
  // workspace deep-linkable and, crucially, lets the browser Back button / breadcrumb from a dataset
  // detail page return to the *tab you left from* (e.g. Datasets), not always Versions.
  // Explicit path navigation (not `navigate([])`) — a relative empty-command nav can dedupe to a
  // no-op on a `:id`-param route, leaving the query param off the URL.
  protected selectTab(t: WorkspaceTab): void {
    this.tab.set(t);
    void this.router.navigate(['/prompts', this.id], {
      queryParams: { tab: t },
      replaceUrl: true,
    });
  }

  private readonly tabIds: WorkspaceTab[] = ['versions', 'datasets', 'analytics', 'runs'];

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    // Restore the tab from the URL so a deep-link / Back-navigation lands on the right tab.
    const urlTab = this.route.snapshot.queryParamMap.get('tab') as WorkspaceTab | null;
    if (urlTab && this.tabIds.includes(urlTab)) this.tab.set(urlTab);
    this.load();
    this.loadDatasets();
    this.modelsApi.listModels().subscribe({ next: (m) => this.models.set(m) });
  }

  private load(): void {
    this.api.getPrompt(this.id).subscribe({
      next: (p) => {
        this.prompt.set(p);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load the prompt.');
        this.loading.set(false);
      },
    });
    this.loadVersionStatus();
  }

  /** The derived per-version status (1.16). Non-fatal on error — the version list still renders. */
  private loadVersionStatus(): void {
    this.api.getVersionStatus(this.id).subscribe({
      next: (s) => this.versionStatus.set(s),
      error: () => {
        /* leave status null; rows simply show no badges */
      },
    });
  }

  /**
   * Mark a version "Current in source" (1.16) — also the mark-as-backported action. Updates the
   * badges from the recomputed status the endpoint returns, and reloads the prompt for the pointer/SHA.
   */
  protected setCurrent(versionId: string): void {
    if (this.settingCurrentId()) return;
    this.settingCurrentId.set(versionId);
    this.error.set(null);
    this.api.setCurrentVersion(this.id, versionId).subscribe({
      next: (status) => {
        this.settingCurrentId.set(null);
        this.versionStatus.set(status);
        this.load(); // refresh the prompt's currentVersionId / SHA
      },
      error: () => {
        this.settingCurrentId.set(null);
        this.error.set('Could not update the current version.');
      },
    });
  }

  // Backport assistance (1.20): the generated artifact for the single backport target, shown in a
  // drawer with copy-exact-prompt + download-markdown. Fetched on demand (only when the maintainer
  // asks) so the workspace load stays lean.
  protected readonly showBackport = signal(false);
  protected readonly backportArtifact = signal<BackportArtifact | null>(null);
  protected readonly backportLoading = signal(false);
  protected readonly backportError = signal<string | null>(null);
  protected readonly copied = signal(false);

  /** Open the drawer and fetch the artifact for this prompt's backport target. */
  protected prepareBackport(): void {
    this.showBackport.set(true);
    this.backportArtifact.set(null);
    this.backportError.set(null);
    this.copied.set(false);
    this.backportLoading.set(true);
    this.api.getBackportArtifact(this.id).subscribe({
      next: (art) => {
        this.backportArtifact.set(art);
        this.backportLoading.set(false);
      },
      error: () => {
        this.backportError.set('Could not assemble the backport artifact.');
        this.backportLoading.set(false);
      },
    });
  }

  /** Copy the target version's exact content to the clipboard — ready to paste into the source app. */
  protected copyExactPrompt(art: BackportArtifact): void {
    void navigator.clipboard?.writeText(art.content).then(
      () => this.copied.set(true),
      () => this.backportError.set('Could not copy to the clipboard.'),
    );
  }

  /** Download the markdown artifact as a `.md` file. */
  protected downloadMarkdown(art: BackportArtifact): void {
    const blob = new Blob([art.markdown], { type: 'text/markdown' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = art.fileName;
    a.click();
    URL.revokeObjectURL(url);
  }

  private loadDatasets(): void {
    this.datasetsApi.listDatasetsByPrompt(this.id).subscribe({
      next: (list) => {
        this.datasets.set(list);
        this.loadPromptRuns(list);
      },
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

  // The Runs tab lists every run of this prompt across its datasets, newest first — loaded with the
  // workspace so the tab is populated without opening the run form. Bounded by the dataset count.
  private loadPromptRuns(datasets: DatasetSummary[]): void {
    if (datasets.length === 0) {
      this.promptRuns.set([]);
      return;
    }
    this.runsLoading.set(true);
    forkJoin(
      datasets.map((d) =>
        this.evalApi.listRuns(d.id).pipe(
          // Tag each run with its dataset name; a per-dataset failure yields [] (never a dead tab).
          map((runs) => runs.map((r) => ({ ...r, datasetName: d.name }))),
          catchError(() => of([] as (EvalRunSummary & { datasetName: string })[])),
        ),
      ),
    ).subscribe({
      next: (perDataset) => {
        const all = perDataset
          .flat()
          .sort((a, b) => (a.createdAt < b.createdAt ? 1 : a.createdAt > b.createdAt ? -1 : 0));
        this.promptRuns.set(all);
        this.runsLoading.set(false);
      },
      error: () => this.runsLoading.set(false),
    });
  }

  /** The version number for a run's promptVersionId, for a readable run identity in the Runs table. */
  protected versionNumberOf(versionId: string): number | null {
    return this.prompt()?.versions.find((v) => v.id === versionId)?.versionNumber ?? null;
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
