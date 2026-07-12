import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Dataset } from '../dataset';
import { DatasetsApiService } from './datasets-api.service';

type OriginFilter = 'all' | 'Captured' | 'Synthetic';

@Component({
  selector: 'app-dataset-detail',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="panel">
      <a class="back" routerLink="/datasets">← All datasets</a>

      @if (error(); as message) {
        <div class="error-box" data-testid="error">{{ message }}</div>
      }

      @if (dataset(); as d) {
        <header class="panel__head">
          <h1 class="title">{{ d.name }}</h1>
          @if (d.description) {
            <p class="subtitle">{{ d.description }}</p>
          }
        </header>

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
          <p class="empty" data-testid="no-fixtures">No fixtures for this filter.</p>
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
                  <td>{{ f.origin }}</td>
                  <td>{{ f.input }}</td>
                  <td>{{ f.upstreamContext ?? '—' }}</td>
                  <td>{{ f.expectedOutput ?? '—' }}</td>
                  <td>{{ f.seedFixtureId ? 'linked' : '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }

        <h2 class="section-title">Capture a fixture</h2>
        <form class="capture" (submit)="capture($event)">
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

        <h2 class="section-title">Generate synthetic fixtures</h2>
        <p class="subtitle">Seeded from this dataset's captured fixtures; steer with guidance.</p>
        <form class="generate" (submit)="generate($event)">
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
    </section>
  `,
  styleUrl: '../prompts/prompts.css',
})
export class DatasetDetail implements OnInit {
  private readonly api = inject(DatasetsApiService);
  private readonly route = inject(ActivatedRoute);

  protected readonly dataset = signal<Dataset | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly originFilter = signal<OriginFilter>('all');
  protected readonly generating = signal(false);

  protected readonly promptInput = signal('');
  protected readonly slmOutput = signal('');
  protected readonly coverageGoals = signal('');
  protected readonly edgeCases = signal('');
  protected readonly count = signal(5);

  private id = '';

  protected readonly filteredFixtures = computed(() => {
    const fixtures = this.dataset()?.fixtures ?? [];
    const filter = this.originFilter();
    return filter === 'all' ? fixtures : fixtures.filter((f) => f.origin === filter);
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  private load(): void {
    this.api.getDataset(this.id).subscribe({
      next: (d) => this.dataset.set(d),
      error: () => this.error.set('Could not load the dataset.'),
    });
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
}
