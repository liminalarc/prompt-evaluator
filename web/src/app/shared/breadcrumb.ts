import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** One breadcrumb hop. `link` omitted ⇒ rendered as the current (non-navigable) crumb. */
export interface Crumb {
  label: string;
  link?: string | unknown[];
}

/**
 * The shared breadcrumb — a consistent trail across every detail page (before, only PromptList
 * had one). Non-final crumbs with a `link` route; the last is marked `aria-current="page"`.
 */
@Component({
  selector: 'app-breadcrumb',
  imports: [RouterLink],
  template: `
    <nav class="breadcrumb" data-testid="breadcrumb" aria-label="Breadcrumb">
      @for (c of items(); track $index; let last = $last) {
        @if (c.link && !last) {
          <a class="breadcrumb__crumb" [routerLink]="c.link" data-testid="crumb">{{ c.label }}</a>
        } @else {
          <span
            class="breadcrumb__crumb breadcrumb__crumb--current"
            data-testid="crumb"
            [attr.aria-current]="last ? 'page' : null"
            >{{ c.label }}</span
          >
        }
        @if (!last) {
          <span class="breadcrumb__sep" aria-hidden="true">/</span>
        }
      }
    </nav>
  `,
  styles: [
    `
      .breadcrumb {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: var(--sb-space-xs);
        font-size: var(--sb-type-small-size);
        margin-bottom: var(--sb-space-md);
      }
      .breadcrumb__crumb {
        color: var(--sb-text-secondary);
        text-decoration: none;
      }
      a.breadcrumb__crumb:hover {
        color: var(--sb-text);
        text-decoration: underline;
      }
      .breadcrumb__crumb--current {
        color: var(--sb-text);
        font-weight: var(--sb-type-h4-weight);
      }
      .breadcrumb__sep {
        color: var(--sb-text-faint);
      }
    `,
  ],
})
export class Breadcrumb {
  readonly items = input<Crumb[]>([]);
}
