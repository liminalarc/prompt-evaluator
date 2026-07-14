import { Component, input } from '@angular/core';

/**
 * The standard in-flight state — a polite status region so pages announce loading instead of
 * rendering blank until their signals resolve. Drop it in wherever data is being fetched.
 */
@Component({
  selector: 'app-loading-state',
  template: `
    <div class="loading" role="status" aria-live="polite" data-testid="loading">
      <span class="loading__spinner" aria-hidden="true"></span>
      <span class="loading__label">{{ label() }}</span>
    </div>
  `,
  styles: [
    `
      .loading {
        display: flex;
        align-items: center;
        gap: var(--sb-space-sm);
        padding: var(--sb-space-lg) 0;
        color: var(--sb-text-secondary);
        font-size: var(--sb-type-small-size);
      }
      .loading__spinner {
        width: 1rem;
        height: 1rem;
        border: 2px solid var(--sb-border);
        border-top-color: var(--sb-primary);
        border-radius: var(--sb-radius-full);
        animation: loading-spin 0.7s linear infinite;
      }
      @keyframes loading-spin {
        to {
          transform: rotate(360deg);
        }
      }
      @media (prefers-reduced-motion: reduce) {
        .loading__spinner {
          animation: none;
        }
      }
    `,
  ],
})
export class LoadingState {
  readonly label = input('Loading…');
}
