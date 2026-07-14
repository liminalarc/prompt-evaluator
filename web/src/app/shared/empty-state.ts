import { Component, input } from '@angular/core';

/**
 * The standard empty state — a quiet message plus an optional projected call-to-action
 * (`<app-empty-state><button>Add…</button></app-empty-state>`). Replaces the ad-hoc `.empty`
 * paragraphs scattered across pages.
 */
@Component({
  selector: 'app-empty-state',
  template: `
    <div class="empty-state" data-testid="empty">
      <p class="empty-state__message">{{ message() }}</p>
      <ng-content />
    </div>
  `,
  styles: [
    `
      .empty-state {
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        gap: var(--sb-space-sm);
        padding: var(--sb-space-xl);
        border: 1px dashed var(--sb-border);
        border-radius: var(--sb-radius-md);
        background: var(--sb-surface-dim);
      }
      .empty-state__message {
        margin: 0;
        color: var(--sb-text-secondary);
        font-size: var(--sb-type-small-size);
      }
    `,
  ],
})
export class EmptyState {
  readonly message = input('');
}
