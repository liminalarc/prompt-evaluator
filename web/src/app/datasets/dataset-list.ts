import { Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { DatasetSummary } from '../dataset';
import { PromptsApiService } from '../prompts/prompts-api.service';
import { OrgContextStore } from '../shared/org-context.store';
import { DatasetsApiService } from './datasets-api.service';

@Component({
  selector: 'app-dataset-list',
  imports: [RouterLink],
  template: `
    <section class="panel">
      <header class="panel__head">
        <h1 class="title">Datasets</h1>
        <p class="subtitle">
          Every dataset lives with a prompt — create and manage them from the prompt's workspace.
          This is the cross-prompt browse view for the current organization.
        </p>
      </header>

      @if (error(); as message) {
        <div class="error-box" data-testid="error">{{ message }}</div>
      }

      @if (datasets(); as list) {
        @if (list.length === 0) {
          <p class="empty" data-testid="empty">
            No datasets yet — open a prompt and add one in its workspace.
          </p>
        } @else {
          <table class="sb-table" data-testid="datasets">
            <thead>
              <tr>
                <th>Name</th>
                <th>Fixtures</th>
                <th>Captured</th>
                <th>Synthetic</th>
              </tr>
            </thead>
            <tbody>
              @for (d of list; track d.id) {
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
    </section>
  `,
  styleUrl: '../prompts/prompts.css',
})
export class DatasetList {
  private readonly api = inject(DatasetsApiService);
  private readonly promptsApi = inject(PromptsApiService);
  private readonly orgStore = inject(OrgContextStore);

  protected readonly datasets = signal<DatasetSummary[] | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // Scope the browse list to the active org — datasets belonging to the org's prompts.
    effect(() => {
      const orgId = this.orgStore.currentOrgId();
      this.datasets.set(null);
      if (orgId) {
        this.load(orgId);
      }
    });
  }

  private load(orgId: string): void {
    forkJoin({
      prompts: this.promptsApi.listPromptsByOrganization(orgId),
      datasets: this.api.listDatasets(),
    }).subscribe({
      next: ({ prompts, datasets }) => {
        const orgPromptIds = new Set(prompts.map((p) => p.id));
        this.datasets.set(datasets.filter((d) => orgPromptIds.has(d.promptId)));
      },
      error: () => this.error.set('Could not load datasets — is the stack running?'),
    });
  }
}
