import { Component, ElementRef, inject, input, output } from '@angular/core';

/**
 * Shared right-side slide-over **Drawer** (2.19 D1). One primitive for the heavy / focused /
 * occasional surfaces (scorer-edit, user↔org management, unified Compare) so the page behind stays
 * a lean hub instead of reflowing tall inline forms. Consistent by construction: right side, fixed
 * width (full-width on narrow), a header with title + close, Esc / scrim-click to close, and an
 * internally-scrolling body. Content is projected — the drawer owns only the chrome.
 *
 * Deliberately *not* for small inline add/edit forms (those keep the 2.4/2.8 reveal pattern) and
 * *not* for always-on primary data. Callers own the open state (a signal) and linkable/URL sync.
 */
@Component({
  selector: 'app-drawer',
  template: `
    @if (open()) {
      <div class="drawer-scrim" data-testid="drawer-scrim" (click)="close()">
        <aside
          class="drawer"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="heading()"
          data-testid="drawer"
          (click)="$event.stopPropagation()"
          (keydown.escape)="close()"
          (keydown.tab)="trapTab($event)"
        >
          <header class="drawer__head">
            <h2 class="drawer__title">{{ heading() }}</h2>
            <button
              type="button"
              class="drawer__close"
              aria-label="Close"
              data-testid="drawer-close"
              (click)="close()"
            >
              ✕
            </button>
          </header>
          <div class="drawer__body">
            <ng-content />
          </div>
        </aside>
      </div>
    }
  `,
  styles: [
    `
      .drawer-scrim {
        position: fixed;
        inset: 0;
        z-index: 1000;
        display: flex;
        justify-content: flex-end;
        background: rgb(0 0 0 / 0.5);
      }
      .drawer {
        width: 40rem;
        max-width: 100%;
        height: 100%;
        display: flex;
        flex-direction: column;
        background: var(--sb-surface);
        border-left: 1px solid var(--sb-border);
        box-shadow: var(--sb-shadow-lg);
        animation: drawer-in 0.18s ease-out;
      }
      @keyframes drawer-in {
        from {
          transform: translateX(1rem);
          opacity: 0.6;
        }
        to {
          transform: translateX(0);
          opacity: 1;
        }
      }
      .drawer__head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--sb-space-md);
        padding: var(--sb-space-lg) var(--sb-space-xl);
        border-bottom: 1px solid var(--sb-border);
      }
      .drawer__title {
        margin: 0;
        font-size: var(--sb-type-h3-size);
        font-weight: var(--sb-type-h3-weight);
        color: var(--sb-text);
      }
      .drawer__close {
        border: none;
        background: transparent;
        color: var(--sb-text-muted);
        font-size: var(--sb-type-body-size);
        cursor: pointer;
        padding: var(--sb-space-xs) var(--sb-space-sm);
        border-radius: var(--sb-radius-sm);
        line-height: 1;
      }
      .drawer__close:hover {
        color: var(--sb-text);
        background: var(--sb-surface-variant);
      }
      .drawer__body {
        flex: 1;
        min-height: 0;
        overflow: auto;
        padding: var(--sb-space-lg) var(--sb-space-xl);
      }
      @media (max-width: 640px) {
        .drawer {
          width: 100%;
          border-left: none;
        }
      }
    `,
  ],
})
export class Drawer {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** Caller-owned open state (a signal). The drawer renders only while true. */
  readonly open = input.required<boolean>();
  readonly heading = input<string>('');
  /** Emitted on Esc, scrim click, or the close button — the caller flips its open signal. */
  readonly closed = output<void>();

  protected close(): void {
    this.closed.emit();
  }

  // Minimal focus trap: keep Tab / Shift+Tab cycling within the panel so keyboard focus can't slip
  // to the page behind. Queries live focusable descendants each press (the projected form varies).
  protected trapTab(rawEvent: Event): void {
    const event = rawEvent as KeyboardEvent;
    const panel = this.host.nativeElement.querySelector('.drawer');
    if (!panel) return;
    const nodes = Array.from(
      panel.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
      ),
    ).filter((el) => el.offsetParent !== null);
    if (nodes.length === 0) return;
    const first = nodes[0];
    const last = nodes[nodes.length - 1];
    const active = document.activeElement as HTMLElement | null;
    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
