import { Component, input } from '@angular/core';
import { Chip } from './chip';

/**
 * A gapped, wrapping row of {@link Chip}s from a list of labels — the one place to render a set of
 * categorical tags (scorer kinds, model roles, …). Fixes the "LlmJudgeRegex" / "subjectjudgegenerator"
 * concatenation: bare adjacent `.sb-chip` spans have no separation, so a list read as one word (2.19
 * W25/W35). Renders an em-dash when empty.
 */
@Component({
  selector: 'app-chip-list',
  imports: [Chip],
  template: `
    @for (label of labels(); track label) {
      <app-chip [label]="label" />
    } @empty {
      <span class="chip-list__empty" data-testid="chip-list-empty">—</span>
    }
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        flex-wrap: wrap;
        gap: var(--sb-space-xs);
        align-items: center;
      }
      .chip-list__empty {
        color: var(--sb-text-muted);
      }
    `,
  ],
})
export class ChipList {
  readonly labels = input.required<readonly string[]>();
}
