import { Component, computed, input, signal } from '@angular/core';
import { DiffGap, DiffRow, collapseUnchanged, diffLines } from './diff';

/**
 * Presentational line-level diff between two prompt-version bodies. Dumb inputs-in, no
 * data fetching — the container decides which two versions to compare. Unchanged runs far from any
 * change collapse into an expandable gap (2.19 W8) so a small edit in a long prompt is scannable.
 */
@Component({
  selector: 'app-version-diff',
  template: `
    <div class="diff" data-testid="diff">
      @for (row of rows(); track $index; let idx = $index) {
        @if (asGap(row); as gap) {
          @if (expanded().has(idx)) {
            @for (line of gap.lines; track $index) {
              <span class="diff__line diff__line--context">&nbsp;&nbsp;{{ line.text }}</span>
            }
          } @else {
            <button type="button" class="diff__gap" data-testid="diff-gap" (click)="expand(idx)">
              ⋯ {{ gap.count }} unchanged line{{ gap.count === 1 ? '' : 's' }}
            </button>
          }
        } @else {
          <span class="diff__line diff__line--{{ row.kind }}"
            >{{ marker(row.kind) }} {{ $any(row).text }}</span
          >
        }
      }
    </div>
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
      .diff__gap {
        display: block;
        width: 100%;
        text-align: left;
        padding: 2px var(--sb-space-sm);
        margin: 2px 0;
        border: none;
        border-radius: var(--sb-radius-sm);
        background: var(--sb-surface-variant);
        color: var(--sb-text-muted);
        font-family: inherit;
        font-size: inherit;
        cursor: pointer;
      }
      .diff__gap:hover {
        color: var(--sb-text);
      }
    `,
  ],
})
export class VersionDiff {
  readonly before = input<string>('');
  readonly after = input<string>('');

  protected readonly rows = computed<DiffRow[]>(() =>
    collapseUnchanged(diffLines(this.before(), this.after())),
  );

  // Which gap rows the user has expanded (by row index). Resets naturally as `rows` recomputes.
  protected readonly expanded = signal<Set<number>>(new Set());

  protected asGap(row: DiffRow): DiffGap | null {
    return row.kind === 'gap' ? row : null;
  }

  protected expand(idx: number): void {
    const next = new Set(this.expanded());
    next.add(idx);
    this.expanded.set(next);
  }

  protected marker(kind: string): string {
    return kind === 'added' ? '+' : kind === 'removed' ? '-' : ' ';
  }
}
