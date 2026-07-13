import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatasetSummary } from '../dataset';
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
          This is the cross-prompt browse view.
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
export class DatasetList implements OnInit {
  private readonly api = inject(DatasetsApiService);

  protected readonly datasets = signal<DatasetSummary[] | null>(null);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.listDatasets().subscribe({
      next: (list) => this.datasets.set(list),
      error: () => this.error.set('Could not load datasets — is the stack running?'),
    });
  }
}
