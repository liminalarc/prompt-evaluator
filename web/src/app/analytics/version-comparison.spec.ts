import { TestBed } from '@angular/core/testing';
import { VersionComparison } from './version-comparison';
import { VersionComparison as VersionComparisonData } from '../analytics';

const data: VersionComparisonData = {
  fromVersionId: 'v1',
  fromVersionNumber: 1,
  fromVersionLabel: 'baseline',
  fromRunId: 'r1',
  toVersionId: 'v2',
  toVersionNumber: 2,
  toVersionLabel: null,
  toRunId: 'r2',
  scorers: [
    {
      scorer: { identity: 'abc', kind: 'FuzzyMatch', judgeModel: null },
      fromMean: 0.7,
      toMean: 0.75,
      delta: 0.05,
      fixtures: [
        {
          fixtureId: 'aaaaaaaa-0000-0000-0000-000000000000',
          fromValue: 0.9,
          toValue: 0.7,
          delta: -0.2,
        },
        {
          fixtureId: 'bbbbbbbb-0000-0000-0000-000000000000',
          fromValue: 0.5,
          toValue: 0.8,
          delta: 0.3,
        },
      ],
    },
  ],
};

describe('VersionComparison', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [VersionComparison] }).compileComponents();
  });

  it('renders nothing until data is provided', () => {
    const fixture = TestBed.createComponent(VersionComparison);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="scorer-comparison"]')).toBeNull();
  });

  it('renders per-scorer aggregate and per-fixture deltas', () => {
    const fixture = TestBed.createComponent(VersionComparison);
    fixture.componentInstance.comparison = data;
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[data-testid="scorer-comparison"]')).toBeTruthy();
    const aggregate = el.querySelector('[data-testid="aggregate-delta"]')!;
    expect(aggregate.textContent).toContain('▲'); // 0.05 improvement
    expect(aggregate.textContent).toContain('0.050');

    const rows = el.querySelectorAll('[data-testid="fixture-delta-row"]');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('▼'); // -0.2 regression on the first fixture
    expect(rows[1].textContent).toContain('▲'); // +0.3 improvement on the second
  });

  it('shows an empty state when there are no overlapping scorers', () => {
    const fixture = TestBed.createComponent(VersionComparison);
    fixture.componentInstance.comparison = { ...data, scorers: [] };
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="comparison-empty"]')).toBeTruthy();
  });
});
