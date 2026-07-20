import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { COMPOSITE_SERIES_NAME, TrendChart } from './trend-chart';
import { CompositeTrendPoint, TrendSeries } from '../analytics';

const series: TrendSeries[] = [
  {
    scorer: { identity: 'abc', kind: 'LlmJudge', judgeModel: 'claude-opus-4-8' },
    points: [
      {
        promptVersionId: 'v1',
        versionNumber: 1,
        versionLabel: 'baseline',
        runId: 'r1',
        runAt: '',
        meanValue: 0.9,
        passRate: 1,
        fixtureCount: 4,
      },
      {
        promptVersionId: 'v2',
        versionNumber: 2,
        versionLabel: null,
        runId: 'r2',
        runAt: '',
        meanValue: 0.5,
        passRate: 0.5,
        fixtureCount: 4,
      },
    ],
  },
];

describe('TrendChart', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TrendChart],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  it('shows an empty state when there is no data', () => {
    const fixture = TestBed.createComponent(TrendChart);
    fixture.componentInstance.series = [];
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="chart-empty"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="trend-chart"]')).toBeNull();
  });

  it('renders a chart when series are present', () => {
    const fixture = TestBed.createComponent(TrendChart);
    fixture.componentInstance.series = series;
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="trend-chart"]')).toBeTruthy();
    expect(el.querySelector('ngx-charts-line-chart')).toBeTruthy();
  });

  it('adds the weighted-composite as an extra distinct line [2.9]', () => {
    const composite: CompositeTrendPoint[] = [
      {
        promptVersionId: 'v1',
        versionNumber: 1,
        versionLabel: 'baseline',
        runId: 'r1',
        runAt: '',
        compositeValue: 0.7,
        scorerCount: 2,
      },
    ];
    const fixture = TestBed.createComponent(TrendChart);
    fixture.componentInstance.series = series;
    fixture.componentInstance.composite = composite;
    fixture.detectChanges();

    // The composite is present as its own named series alongside the scorer line.
    const data = (
      fixture.componentInstance as unknown as { chartData(): { name: string }[] }
    ).chartData();
    expect(data.map((s) => s.name)).toContain(COMPOSITE_SERIES_NAME);
  });
});
