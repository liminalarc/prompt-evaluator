import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PromptSummary } from '../prompt';
import { PromptsApiService } from './prompts-api.service';

@Component({
  selector: 'app-prompt-list',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="panel">
      <header class="panel__head">
        <h1 class="title">Prompts</h1>
        <p class="subtitle">The registry — each prompt keeps an append-only version history.</p>
      </header>

      <form class="create" (submit)="create($event)">
        <div class="sb-field">
          <label for="name">New prompt name</label>
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
          Create prompt
        </button>
      </form>

      @if (error(); as message) {
        <div class="error-box" data-testid="error">{{ message }}</div>
      }

      @if (prompts(); as list) {
        @if (list.length === 0) {
          <p class="empty" data-testid="empty">No prompts yet — create one above.</p>
        } @else {
          <table class="sb-table" data-testid="prompts">
            <thead>
              <tr>
                <th>Name</th>
                <th>Versions</th>
                <th>Latest target model</th>
              </tr>
            </thead>
            <tbody>
              @for (p of list; track p.id) {
                <tr>
                  <td>
                    <a [routerLink]="['/prompts', p.id]">{{ p.name }}</a>
                  </td>
                  <td>{{ p.versionCount }}</td>
                  <td>{{ p.latestTargetModel ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      }
    </section>
  `,
  styleUrl: './prompts.css',
})
export class PromptList implements OnInit {
  private readonly api = inject(PromptsApiService);

  protected readonly prompts = signal<PromptSummary[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly name = signal('');
  protected readonly description = signal('');

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.api.listPrompts().subscribe({
      next: (list) => this.prompts.set(list),
      error: () => this.error.set('Could not load prompts — is the stack running?'),
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
    this.api.createPrompt(name, description).subscribe({
      next: () => {
        this.name.set('');
        this.description.set('');
        this.load();
      },
      error: () => this.error.set('Could not create the prompt.'),
    });
  }
}
