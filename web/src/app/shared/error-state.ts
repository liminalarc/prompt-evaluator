import { Component, input, output } from '@angular/core';

/**
 * The standard error banner — a brand field-error alert (`.sb-field--error`) with an optional
 * retry. Replaces the ad-hoc `.error-box` "is the stack running?" one-liners; keeps
 * `data-testid="error"` so existing selectors keep working.
 */
@Component({
  selector: 'app-error-state',
  template: `
    <div class="sb-field--error error-state" role="alert" data-testid="error">
      <span class="error-state__message">{{ message() }}</span>
      @if (retryable()) {
        <button
          class="sb-btn sb-btn--sm sb-btn--secondary"
          type="button"
          data-testid="error-retry"
          (click)="retry.emit()"
        >
          Retry
        </button>
      }
    </div>
  `,
  styles: [
    `
      .error-state {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--sb-space-md);
        padding: var(--sb-space-md) var(--sb-space-lg);
        border-radius: var(--sb-radius-md);
      }
      .error-state__message {
        font-size: var(--sb-type-small-size);
      }
    `,
  ],
})
export class ErrorState {
  readonly message = input.required<string>();
  readonly retryable = input(false);
  readonly retry = output<void>();
}
