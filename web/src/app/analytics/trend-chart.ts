import { Component, Input, computed, signal } from '@angular/core';
import { Color, LegendPosition, LineChartModule, ScaleType } from '@swimlane/ngx-charts';
import { CompositeTrendPoint, TrendSeries, scorerLabel } from '../analytics';

/** The composite line's fixed series label, kept distinct from any scorer name. */
export const COMPOSITE_SERIES_NAME = 'Weighted composite';

// Categorical hues, in fixed order, sourced from the brand tokens (validated colorblind-safe in
// light mode; dark mode passes CVD/chroma/contrast with secondary encoding — legend + markers).
// Status tokens (success/error/warning) are deliberately excluded — they are reserved.
const CATEGORICAL_TOKENS = ['--sb-primary', '--sb-accent', '--sb-ai', '--sb-info'];

interface ChartSeries {
  name: string;
  series: { name: string; value: number }[];
}

/**
 * Presentational trend chart: score (0–1) per prompt version, one line per scorer series. Colors
 * come from the brand tokens at runtime so the chart tracks the active light/dark theme. Dumb —
 * data in, nothing out.
 */
@Component({
  selector: 'app-trend-chart',
  imports: [LineChartModule],
  template: `
    @if (chartData().length === 0) {
      <p class="empty" data-testid="chart-empty">
        No trend data yet — run this prompt over the dataset.
      </p>
    } @else {
      <div class="chart" data-testid="trend-chart">
        <ngx-charts-line-chart
          [results]="chartData()"
          [scheme]="scheme()"
          [xAxis]="true"
          [yAxis]="true"
          [showXAxisLabel]="true"
          [showYAxisLabel]="true"
          xAxisLabel="Version"
          yAxisLabel="Mean score"
          [legend]="chartData().length > 1"
          legendTitle="Scorer"
          [legendPosition]="legendBelow"
          [yScaleMin]="0"
          [yScaleMax]="1"
          [roundDomains]="true"
          [autoScale]="false"
        />
      </div>
    }
  `,
  styleUrl: './trend-chart.css',
})
export class TrendChart {
  private readonly _series = signal<TrendSeries[]>([]);
  private readonly _composite = signal<CompositeTrendPoint[]>([]);

  protected readonly legendBelow = LegendPosition.Below;

  @Input() set series(value: TrendSeries[] | null) {
    this._series.set(value ?? []);
  }

  /** The weighted-composite series (2.9), drawn as an extra distinct line alongside the scorers. */
  @Input() set composite(value: CompositeTrendPoint[] | null) {
    this._composite.set(value ?? []);
  }

  protected readonly chartData = computed<ChartSeries[]>(() => {
    const versionName = (n: number, label: string | null) => (label ? `v${n} · ${label}` : `v${n}`);

    const scorerLines = this._series().map((s) => ({
      name: scorerLabel(s.scorer),
      series: s.points.map((p) => ({
        name: versionName(p.versionNumber, p.versionLabel),
        value: p.meanValue,
      })),
    }));

    const composite = this._composite();
    if (composite.length > 0) {
      scorerLines.push({
        name: COMPOSITE_SERIES_NAME,
        series: composite.map((p) => ({
          name: versionName(p.versionNumber, p.versionLabel),
          value: p.compositeValue,
        })),
      });
    }
    return scorerLines;
  });

  protected readonly scheme = computed<Color>(() => ({
    name: 'brand',
    selectable: true,
    group: ScaleType.Ordinal,
    domain: this.readTokenColors(),
  }));

  private readTokenColors(): string[] {
    if (typeof document === 'undefined' || typeof getComputedStyle !== 'function') {
      return CATEGORICAL_TOKENS; // non-DOM context (e.g. SSR): ngx-charts will fall back
    }
    const styles = getComputedStyle(document.documentElement);
    return CATEGORICAL_TOKENS.map((token) => styles.getPropertyValue(token).trim()).filter(Boolean);
  }
}
