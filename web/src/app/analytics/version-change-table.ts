import { Component, Input, computed, signal } from '@angular/core';
import { CompositeTrendPoint, TrendSeries, scorerLabel } from '../analytics';

/** One scorer/composite cell in the change table: its value at a version and the Δ vs the prior version. */
interface ChangeCell {
  value: number | null;
  delta: number | null;
}

/** One row of the change table — a version, with a cell per scorer plus the composite. */
interface ChangeRow {
  versionNumber: number;
  versionLabel: string | null;
  scorerCells: ChangeCell[];
  composite: ChangeCell;
}

/**
 * Version-over-version change table (2.9): rows = a prompt×dataset's versions (ascending), columns =
 * each scorer's mean plus the weighted composite, each with its delta vs the prior version.
 * Extends 2.8's two-version compare to N versions. Presentational — data in, nothing out; fed the
 * same trend + composite series the chart draws.
 */
@Component({
  selector: 'app-version-change-table',
  imports: [],
  template: `
    @if (rows().length === 0) {
      <p class="empty" data-testid="change-table-empty">
        No runs yet — run this prompt over the dataset to see version-over-version change.
      </p>
    } @else {
      <div class="table-scroll">
        <table class="sb-table" data-testid="change-table">
          <thead>
            <tr>
              <th>Version</th>
              @for (c of columns(); track c) {
                <th>{{ c }}</th>
              }
              <th>Composite</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.versionNumber) {
              <tr data-testid="change-row">
                <td>
                  v{{ row.versionNumber }}{{ row.versionLabel ? ' · ' + row.versionLabel : '' }}
                </td>
                @for (cell of row.scorerCells; track $index) {
                  <td>{{ fmt(cell) }}</td>
                }
                <td data-testid="composite-cell" class="composite-cell">
                  {{ fmt(row.composite) }}
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [
    `
      .table-scroll {
        overflow-x: auto;
      }
      .composite-cell {
        font-weight: 600;
      }
      .empty {
        color: var(--sb-text-muted);
      }
    `,
  ],
})
export class VersionChangeTable {
  private readonly _series = signal<TrendSeries[]>([]);
  private readonly _composite = signal<CompositeTrendPoint[]>([]);

  @Input() set series(value: TrendSeries[] | null) {
    this._series.set(value ?? []);
  }

  @Input() set composite(value: CompositeTrendPoint[] | null) {
    this._composite.set(value ?? []);
  }

  /** Column headers, one per scorer series (composite is a fixed trailing column). */
  protected readonly columns = computed(() => this._series().map((s) => scorerLabel(s.scorer)));

  protected readonly rows = computed<ChangeRow[]>(() => {
    const series = this._series();
    const composite = this._composite();

    // Every version that appears in any series or the composite, with its label, ascending.
    const versions = new Map<number, string | null>();
    for (const s of series) for (const p of s.points) versions.set(p.versionNumber, p.versionLabel);
    for (const p of composite) versions.set(p.versionNumber, p.versionLabel);
    const ordered = [...versions.keys()].sort((a, b) => a - b);

    // versionNumber → value, per scorer and for the composite.
    const scorerMaps = series.map(
      (s) => new Map(s.points.map((p) => [p.versionNumber, p.meanValue] as const)),
    );
    const compositeMap = new Map(
      composite.map((p) => [p.versionNumber, p.compositeValue] as const),
    );

    const cell = (map: Map<number, number>, version: number, prior: number | null): ChangeCell => {
      const value = map.has(version) ? map.get(version)! : null;
      const priorValue = prior != null && map.has(prior) ? map.get(prior)! : null;
      const delta = value != null && priorValue != null ? value - priorValue : null;
      return { value, delta };
    };

    return ordered.map((version, i) => {
      const prior = i > 0 ? ordered[i - 1] : null;
      return {
        versionNumber: version,
        versionLabel: versions.get(version) ?? null,
        scorerCells: scorerMaps.map((m) => cell(m, version, prior)),
        composite: cell(compositeMap, version, prior),
      };
    });
  });

  /** "0.720 (+0.050)" — the value with a signed delta vs the prior version; "—" when absent. */
  protected fmt(cell: ChangeCell): string {
    if (cell.value == null) return '—';
    const value = cell.value.toFixed(3);
    if (cell.delta == null) return value;
    const sign = cell.delta >= 0 ? '+' : '';
    return `${value} (${sign}${cell.delta.toFixed(3)})`;
  }
}
