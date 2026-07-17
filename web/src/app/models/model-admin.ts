import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ModelCatalogEntry, ModelRole } from '../model';
import {
  Breadcrumb,
  Card,
  Chip,
  Crumb,
  EmptyState,
  ErrorState,
  LoadingState,
  PageHeader,
  StatusBadge,
} from '../shared';
import { ModelsApiService, ModelWriteBody } from './models-api.service';

const PROVIDERS = ['Anthropic', 'OpenAi'];

/**
 * Admin management for the Model Catalog (spec 1.13). Reached at /admin/models and gated to
 * global admins (route + nav). Non-admins never see it; they only consume the droplists.
 */
@Component({
  selector: 'app-model-admin',
  imports: [
    FormsModule,
    Breadcrumb,
    Card,
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

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      <app-page-header
        heading="Model catalog"
        subtitle="Manage the models offered in the target and judge droplists."
      />

      <app-card heading="Models">
        @if (loading()) {
          <app-loading-state label="Loading models…" />
        } @else if (models().length === 0) {
          <app-empty-state message="No models yet — add the first with the button below." />
        } @else {
          <table class="sb-table" data-testid="models-admin-table">
            <thead>
              <tr>
                <th>Model</th>
                <th>Provider</th>
                <th>Roles</th>
                <th>Pricing (in/out $/MTok)</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (m of models(); track m.id) {
                <tr [attr.data-model-id]="m.modelId">
                  <td>
                    <strong>{{ m.displayName }}</strong>
                    <div class="model-id">{{ m.modelId }}</div>
                  </td>
                  <td>{{ m.provider }}</td>
                  <td>
                    @for (r of m.roles; track r) {
                      <app-chip [label]="r" />
                    }
                  </td>
                  <td>
                    {{ m.inputPricePerMTokUsd ?? '—' }} / {{ m.outputPricePerMTokUsd ?? '—' }}
                  </td>
                  <td>
                    <app-status-badge
                      [variant]="m.isActive ? 'success' : 'neutral'"
                      [label]="m.isActive ? 'Active' : 'Inactive'"
                    />
                  </td>
                  <td class="row-actions">
                    <button
                      class="sb-btn sb-btn--sm sb-btn--secondary"
                      type="button"
                      data-testid="edit-model"
                      (click)="startEdit(m)"
                    >
                      Edit
                    </button>
                    <button
                      class="sb-btn sb-btn--sm sb-btn--secondary"
                      type="button"
                      data-testid="toggle-active"
                      (click)="toggleActive(m)"
                    >
                      {{ m.isActive ? 'Deactivate' : 'Activate' }}
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }

        @if (showForm()) {
          <form class="form-stack model-form" (submit)="save($event)" data-testid="model-form">
            <div class="sb-field">
              <label for="modelId">Model id</label>
              <input
                id="modelId"
                name="modelId"
                data-testid="model-id"
                [ngModel]="fModelId()"
                (ngModelChange)="fModelId.set($event)"
                [disabled]="editingId() !== null"
              />
            </div>
            <div class="sb-field">
              <label for="displayName">Display name</label>
              <input
                id="displayName"
                name="displayName"
                data-testid="model-display-name"
                [ngModel]="fDisplayName()"
                (ngModelChange)="fDisplayName.set($event)"
              />
            </div>
            <div class="sb-field">
              <label for="provider">Provider</label>
              <select
                id="provider"
                name="provider"
                data-testid="model-provider"
                [ngModel]="fProvider()"
                (ngModelChange)="fProvider.set($event)"
              >
                @for (p of providers; track p) {
                  <option [value]="p">{{ p }}</option>
                }
              </select>
            </div>
            <div class="sb-field">
              <span class="field-legend">Roles</span>
              <label class="role-check">
                <input
                  type="checkbox"
                  data-testid="role-subject"
                  [ngModel]="fSubject()"
                  (ngModelChange)="fSubject.set($event)"
                  name="roleSubject"
                />
                Subject
              </label>
              <label class="role-check">
                <input
                  type="checkbox"
                  data-testid="role-judge"
                  [ngModel]="fJudge()"
                  (ngModelChange)="fJudge.set($event)"
                  name="roleJudge"
                />
                Judge
              </label>
              <label class="role-check">
                <input
                  type="checkbox"
                  data-testid="role-generator"
                  [ngModel]="fGenerator()"
                  (ngModelChange)="fGenerator.set($event)"
                  name="roleGenerator"
                />
                Generator
              </label>
            </div>
            <div class="sb-field">
              <label for="inPrice">Input price ($/MTok, optional)</label>
              <input
                id="inPrice"
                name="inPrice"
                type="number"
                min="0"
                step="0.01"
                [ngModel]="fInputPrice()"
                (ngModelChange)="fInputPrice.set($event)"
              />
            </div>
            <div class="sb-field">
              <label for="outPrice">Output price ($/MTok, optional)</label>
              <input
                id="outPrice"
                name="outPrice"
                type="number"
                min="0"
                step="0.01"
                [ngModel]="fOutputPrice()"
                (ngModelChange)="fOutputPrice.set($event)"
              />
            </div>
            <button class="sb-btn sb-btn--primary" type="submit" data-testid="save-model">
              {{ editingId() ? 'Save changes' : 'Add model' }}
            </button>
          </form>
        }

        <button
          foot
          class="sb-btn sb-btn--sm sb-btn--secondary"
          type="button"
          data-testid="toggle-model-form"
          (click)="startCreate()"
        >
          + Add model
        </button>
      </app-card>
    </section>
  `,
  styles: [
    `
      .model-id {
        font-size: var(--sb-type-small-size);
        color: var(--sb-text-secondary);
      }
      .row-actions {
        display: flex;
        gap: var(--sb-space-sm);
      }
      .model-form {
        margin-top: var(--sb-space-lg);
        padding-top: var(--sb-space-lg);
        border-top: 1px solid var(--sb-border);
      }
      .field-legend {
        display: block;
        margin-bottom: var(--sb-space-xs);
        font-size: var(--sb-type-small-size);
        color: var(--sb-text-secondary);
      }
      .role-check {
        display: inline-flex;
        align-items: center;
        gap: var(--sb-space-xs);
        margin-right: var(--sb-space-md);
        font-weight: normal;
      }
    `,
  ],
})
export class ModelAdmin implements OnInit {
  private readonly api = inject(ModelsApiService);

  protected readonly providers = PROVIDERS;
  protected readonly models = signal<ModelCatalogEntry[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly showForm = signal(false);
  protected readonly editingId = signal<string | null>(null);

  protected readonly fModelId = signal('');
  protected readonly fDisplayName = signal('');
  protected readonly fProvider = signal(PROVIDERS[0]);
  protected readonly fSubject = signal(true);
  protected readonly fJudge = signal(false);
  protected readonly fGenerator = signal(false);
  protected readonly fInputPrice = signal('');
  protected readonly fOutputPrice = signal('');

  protected readonly crumbs = computed<Crumb[]>(() => [
    { label: 'Dashboard', link: '/' },
    { label: 'Model catalog' },
  ]);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.api.listAllModels().subscribe({
      next: (m) => {
        this.models.set(m);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load the model catalog.');
        this.loading.set(false);
      },
    });
  }

  protected startCreate(): void {
    this.editingId.set(null);
    this.fModelId.set('');
    this.fDisplayName.set('');
    this.fProvider.set(PROVIDERS[0]);
    this.fSubject.set(true);
    this.fJudge.set(false);
    this.fGenerator.set(false);
    this.fInputPrice.set('');
    this.fOutputPrice.set('');
    this.showForm.set(true);
  }

  protected startEdit(m: ModelCatalogEntry): void {
    this.editingId.set(m.id);
    this.fModelId.set(m.modelId);
    this.fDisplayName.set(m.displayName);
    this.fProvider.set(m.provider);
    this.fSubject.set(m.roles.includes('subject'));
    this.fJudge.set(m.roles.includes('judge'));
    this.fGenerator.set(m.roles.includes('generator'));
    this.fInputPrice.set(m.inputPricePerMTokUsd?.toString() ?? '');
    this.fOutputPrice.set(m.outputPricePerMTokUsd?.toString() ?? '');
    this.showForm.set(true);
  }

  protected save(event: Event): void {
    event.preventDefault();
    const roles = this.selectedRoles();
    if (!this.fDisplayName().trim() || roles.length === 0) {
      this.error.set('A display name and at least one role are required.');
      return;
    }
    const body: ModelWriteBody = {
      displayName: this.fDisplayName().trim(),
      provider: this.fProvider(),
      roles,
      inputPricePerMTokUsd: this.parsePrice(this.fInputPrice()),
      outputPricePerMTokUsd: this.parsePrice(this.fOutputPrice()),
    };
    this.error.set(null);

    const editingId = this.editingId();
    const request$ = editingId
      ? this.api.updateModel(editingId, body)
      : this.api.createModel({ ...body, modelId: this.fModelId().trim() });

    request$.subscribe({
      next: () => {
        this.showForm.set(false);
        this.load();
      },
      error: () =>
        this.error.set('Could not save the model — check the fields (id must be unique).'),
    });
  }

  protected toggleActive(m: ModelCatalogEntry): void {
    this.error.set(null);
    this.api.setActive(m.id, !m.isActive).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Could not change the model status.'),
    });
  }

  private selectedRoles(): ModelRole[] {
    const roles: ModelRole[] = [];
    if (this.fSubject()) roles.push('subject');
    if (this.fJudge()) roles.push('judge');
    if (this.fGenerator()) roles.push('generator');
    return roles;
  }

  private parsePrice(value: string): number | null {
    const trimmed = value.trim();
    if (trimmed === '') return null;
    const n = Number(trimmed);
    return Number.isFinite(n) ? n : null;
  }
}
