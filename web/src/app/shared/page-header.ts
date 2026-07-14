import { Component, input } from '@angular/core';

/**
 * The shared page header — title, optional subtitle, and a projected primary-action slot
 * (`<button actions>…</button>`). Applied to every page so chrome is consistent instead of
 * each page hand-rolling `.panel__head` / `.title`.
 */
@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header" data-testid="page-header">
      <div class="page-header__text">
        <h1 class="page-header__title">{{ heading() }}</h1>
        @if (subtitle()) {
          <p class="page-header__subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="page-header__actions">
        <ng-content select="[actions]" />
      </div>
    </header>
  `,
  styles: [
    `
      .page-header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: var(--sb-space-lg);
        margin-bottom: var(--sb-space-xl);
      }
      .page-header__title {
        margin: 0;
        font-size: var(--sb-type-h1-size);
        font-weight: var(--sb-type-h1-weight);
        color: var(--sb-text);
      }
      .page-header__subtitle {
        margin: var(--sb-space-xs) 0 0;
        color: var(--sb-text-secondary);
        font-size: var(--sb-type-small-size);
      }
      .page-header__actions {
        display: flex;
        gap: var(--sb-space-sm);
        flex-shrink: 0;
      }
    `,
  ],
})
export class PageHeader {
  readonly heading = input.required<string>();
  readonly subtitle = input('');
}
