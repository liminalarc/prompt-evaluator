import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TrendChart } from './trend-chart';
import { TrendSeries } from '../analytics';

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
});
