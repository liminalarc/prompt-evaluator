import { Component, inject } from '@angular/core';
import { ConfirmService } from './confirm.service';

/**
 * The single confirmation dialog (1.10), mounted once in the app shell. It renders whatever
 * {@link ConfirmService} has pending and reports the choice back through the service. Tokenized
 * (brand `--sb-*`), theme-aware, and keyboard-dismissible (Esc / backdrop).
 */
@Component({
  selector: 'app-confirm-dialog',
  template: `
    @if (confirm.request(); as req) {
      <div
        class="scrim"
        data-testid="confirm-dialog"
        (click)="confirm.cancel()"
        (keydown.escape)="confirm.cancel()"
      >
        <div
          class="dialog sb-card"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="confirm-title"
          (click)="$event.stopPropagation()"
        >
          <h3 id="confirm-title" class="dialog__title">{{ req.title }}</h3>
          <p class="dialog__message" data-testid="confirm-message">{{ req.message }}</p>
          <div class="dialog__actions">
            <button
              class="sb-btn sb-btn--secondary"
              type="button"
              data-testid="confirm-cancel"
              (click)="confirm.cancel()"
            >
              Cancel
            </button>
            <button
              class="sb-btn sb-btn--danger"
              type="button"
              data-testid="confirm-delete"
              (click)="confirm.confirm()"
            >
              {{ req.confirmLabel ?? 'Delete' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [
    `
      .scrim {
        position: fixed;
        inset: 0;
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: var(--sb-space-lg);
        /* A neutral dark scrim — token-free by necessity; no brand overlay token exists. */
        background: rgb(0 0 0 / 0.5);
      }
      .dialog {
        max-width: 32rem;
        width: 100%;
        padding: var(--sb-space-lg) var(--sb-space-xl);
        box-shadow: var(--sb-shadow-lg);
      }
      .dialog__title {
        margin: 0 0 var(--sb-space-sm);
        font-size: var(--sb-type-h3-size);
        font-weight: var(--sb-type-h3-weight);
        color: var(--sb-text);
      }
      .dialog__message {
        margin: 0 0 var(--sb-space-lg);
        color: var(--sb-text-secondary);
        font-size: var(--sb-type-body-size);
        line-height: 1.5;
      }
      .dialog__actions {
        display: flex;
        justify-content: flex-end;
        gap: var(--sb-space-sm);
      }
    `,
  ],
})
export class ConfirmDialog {
  protected readonly confirm = inject(ConfirmService);
}
