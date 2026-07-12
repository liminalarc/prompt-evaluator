import { Component, computed, input } from '@angular/core';
import { diffLines } from './diff';

/**
 * Presentational line-level diff between two prompt-version bodies. Dumb inputs-in, no
 * data fetching — the container decides which two versions to compare.
 */
@Component({
  selector: 'app-version-diff',
  template: `
    <pre class="diff" data-testid="diff">@for (line of lines(); track $index) {
<span class="diff__line diff__line--{{ line.kind }}"
  >{{ marker(line.kind) }} {{ line.text }}</span
>}</pre>
  `,
  styles: [
    `
      .diff {
        margin: 0;
        padding: var(--sb-space-md) var(--sb-space-lg);
        border-radius: var(--sb-radius-md);
        background: var(--sb-surface);
        font-family: var(--sb-font-mono);
        font-size: var(--sb-type-small-size);
        overflow-x: auto;
      }
      .diff__line {
        display: block;
        white-space: pre-wrap;
      }
      .diff__line--added {
        background: var(--sb-success-surface);
        color: var(--sb-success-text);
      }
      .diff__line--removed {
        background: var(--sb-error-surface);
        color: var(--sb-error);
      }
      .diff__line--context {
        color: var(--sb-text-secondary);
      }
    `,
  ],
})
export class VersionDiff {
  readonly before = input<string>('');
  readonly after = input<string>('');

  protected readonly lines = computed(() => diffLines(this.before(), this.after()));

  protected marker(kind: 'context' | 'added' | 'removed'): string {
    return kind === 'added' ? '+' : kind === 'removed' ? '-' : ' ';
  }
}
