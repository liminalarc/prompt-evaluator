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
          fixtureLabel: 'mid-cap golfer',
          fromValue: 0.9,
          toValue: 0.7,
          delta: -0.2,
          fromRationale: 'v1 invents a benchmark',
          toRationale: 'v2 is clean — no benchmark',
        },
        {
          fixtureId: 'bbbbbbbb-0000-0000-0000-000000000000',
          fixtureLabel: null,
          fromValue: 0.5,
          toValue: 0.8,
          delta: 0.3,
          fromRationale: null,
          toRationale: null,
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
    expect(rows[0].textContent).toContain('mid-cap golfer'); // label shown, not the GUID (U7)
    expect(rows[0].textContent).not.toContain('aaaaaaaa');
    expect(rows[1].textContent).toContain('bbbbbbbb'); // no label → short-GUID fallback
    expect(rows[1].textContent).toContain('▲'); // +0.3 improvement on the second
  });

  it('expands a fixture to diff the judge rationale on each side [2.14]', () => {
    const fixture = TestBed.createComponent(VersionComparison);
    fixture.componentInstance.comparison = data;
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    // No rationale panel until you ask for the "why".
    expect(el.querySelector('[data-testid="rationale-diff"]')).toBeNull();
    // The fixture with rationales offers a toggle; the one without does not.
    const toggles = el.querySelectorAll('[data-testid="rationale-toggle"]');
    expect(toggles.length).toBe(1);

    (toggles[0] as HTMLButtonElement).click();
    fixture.detectChanges();
    const diff = el.querySelector('[data-testid="rationale-diff"]')!;
    expect(diff.textContent).toContain('v1 invents a benchmark');
    expect(diff.textContent).toContain('v2 is clean — no benchmark');
  });

  it('shows an empty state when there are no overlapping scorers', () => {
    const fixture = TestBed.createComponent(VersionComparison);
    fixture.componentInstance.comparison = { ...data, scorers: [] };
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="comparison-empty"]')).toBeTruthy();
  });
});
