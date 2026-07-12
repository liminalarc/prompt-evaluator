import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Prompt } from '../prompt';
import { PromptsApiService } from './prompts-api.service';
import { VersionDiff } from './version-diff';

@Component({
  selector: 'app-prompt-detail',
  imports: [FormsModule, RouterLink, VersionDiff],
  template: `
    <section class="panel">
      <a class="back" routerLink="/prompts">← All prompts</a>

      @if (error(); as message) {
        <div class="error-box" data-testid="error">{{ message }}</div>
      }

      @if (prompt(); as p) {
        <header class="panel__head">
          <h1 class="title">{{ p.name }}</h1>
          @if (p.description) {
            <p class="subtitle">{{ p.description }}</p>
          }
        </header>

        <h2 class="section-title">Version history</h2>
        @if (p.versions.length === 0) {
          <p class="empty" data-testid="no-versions">No versions yet — add the first below.</p>
        } @else {
          <table class="sb-table" data-testid="versions">
            <thead>
              <tr>
                <th>#</th>
                <th>Target model</th>
                <th>Label</th>
                <th>Source app</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              @for (v of p.versions; track v.id) {
                <tr>
                  <td>{{ v.versionNumber }}</td>
                  <td>{{ v.targetModel }}</td>
                  <td>{{ v.label ?? '—' }}</td>
                  <td>{{ v.sourceApp ?? '—' }}</td>
                  <td>{{ v.createdAt }}</td>
                </tr>
              }
            </tbody>
          </table>
        }

        @if (p.versions.length >= 2) {
          <h2 class="section-title">Compare versions</h2>
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
        }

        <h2 class="section-title">Add a version</h2>
        <form class="add-version" (submit)="addVersion($event)">
          <div class="sb-field">
            <label for="content">Content</label>
            <textarea
              id="content"
              name="content"
              rows="4"
              [ngModel]="content()"
              (ngModelChange)="content.set($event)"
            ></textarea>
          </div>
          <div class="sb-field">
            <label for="targetModel">Target model</label>
            <input
              id="targetModel"
              name="targetModel"
              [ngModel]="targetModel()"
              (ngModelChange)="targetModel.set($event)"
            />
          </div>
          <div class="sb-field">
            <label for="label">Label (optional)</label>
            <input
              id="label"
              name="label"
              [ngModel]="label()"
              (ngModelChange)="label.set($event)"
            />
          </div>
          <button class="sb-btn sb-btn--primary" type="submit" data-testid="add-version">
            Add version
          </button>
        </form>
      }
    </section>
  `,
  styleUrl: './prompts.css',
})
export class PromptDetail implements OnInit {
  private readonly api = inject(PromptsApiService);
  private readonly route = inject(ActivatedRoute);

  protected readonly prompt = signal<Prompt | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly fromNumber = signal(1);
  protected readonly toNumber = signal(1);

  protected readonly content = signal('');
  protected readonly targetModel = signal('claude-sonnet-5');
  protected readonly label = signal('');

  private id = '';

  protected readonly fromContent = computed(() => this.contentOf(this.fromNumber()));
  protected readonly toContent = computed(() => this.contentOf(this.toNumber()));

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  private load(): void {
    this.api.getPrompt(this.id).subscribe({
      next: (p) => {
        this.prompt.set(p);
        if (p.versions.length >= 2) {
          this.fromNumber.set(p.versions[p.versions.length - 2].versionNumber);
          this.toNumber.set(p.versions[p.versions.length - 1].versionNumber);
        }
      },
      error: () => this.error.set('Could not load the prompt.'),
    });
  }

  private contentOf(versionNumber: number): string {
    return this.prompt()?.versions.find((v) => v.versionNumber === versionNumber)?.content ?? '';
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
}
