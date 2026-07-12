import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DatasetSummary } from '../dataset';
import { DatasetsApiService } from './datasets-api.service';

@Component({
  selector: 'app-dataset-list',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="panel">
      <header class="panel__head">
        <h1 class="title">Datasets</h1>
        <p class="subtitle">
          Capture-first fixtures — ground truth from the apps, synthetic to fill the gaps.
        </p>
      </header>

      <form class="create" (submit)="create($event)">
        <div class="sb-field">
          <label for="name">New dataset name</label>
          <input id="name" name="name" [ngModel]="name()" (ngModelChange)="name.set($event)" />
        </div>
        <div class="sb-field">
          <label for="description">Description (optional)</label>
          <input
            id="description"
            name="description"
            [ngModel]="description()"
            (ngModelChange)="description.set($event)"
          />
        </div>
        <button class="sb-btn sb-btn--primary" type="submit" data-testid="create">
          Create dataset
        </button>
      </form>

      @if (error(); as message) {
        <div class="error-box" data-testid="error">{{ message }}</div>
      }

      @if (datasets(); as list) {
        @if (list.length === 0) {
          <p class="empty" data-testid="empty">No datasets yet — create one above.</p>
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
  protected readonly name = signal('');
  protected readonly description = signal('');

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.api.listDatasets().subscribe({
      next: (list) => this.datasets.set(list),
      error: () => this.error.set('Could not load datasets — is the stack running?'),
    });
  }

  protected create(event: Event): void {
    event.preventDefault();
    const name = this.name().trim();
    if (!name) {
      return;
    }
    this.error.set(null);
    const description = this.description().trim() || null;
    this.api.createDataset(name, description).subscribe({
      next: () => {
        this.name.set('');
        this.description.set('');
        this.load();
      },
      error: () => this.error.set('Could not create the dataset.'),
    });
  }
}
