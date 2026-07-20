import { TestBed } from '@angular/core/testing';
import { CompositeTrendPoint, TrendSeries } from '../analytics';
import { VersionChangeTable } from './version-change-table';

describe('VersionChangeTable', () => {
  function render(series: TrendSeries[], composite: CompositeTrendPoint[]) {
    TestBed.configureTestingModule({ imports: [VersionChangeTable] });
    const fixture = TestBed.createComponent(VersionChangeTable);
    fixture.componentRef.setInput('series', series);
    fixture.componentRef.setInput('composite', composite);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const pt = (n: number, mean: number) => ({
    promptVersionId: `v${n}`,
    versionNumber: n,
    versionLabel: null,
    runId: `r${n}`,
    runAt: '',
    meanValue: mean,
    passRate: null,
    fixtureCount: 2,
  });

  const comp = (n: number, value: number): CompositeTrendPoint => ({
    promptVersionId: `v${n}`,
    versionNumber: n,
    versionLabel: null,
    runId: `r${n}`,
    runAt: '',
    compositeValue: value,
    scorerCount: 2,
  });

  it('shows the empty state with no data', () => {
    const el = render([], []);
    expect(el.querySelector('[data-testid="change-table-empty"]')).toBeTruthy();
  });

  it('lays out one row per version with per-scorer + composite deltas vs the prior version', () => {
    const series: TrendSeries[] = [
      {
        scorer: { identity: 'j', kind: 'LlmJudge', judgeModel: 'claude-opus-4-8' },
        points: [pt(1, 0.5), pt(2, 0.9)],
      },
    ];
    const composite = [comp(1, 0.5), comp(2, 0.8)];

    const el = render(series, composite);
    const rows = el.querySelectorAll('[data-testid="change-row"]');
    expect(rows.length).toBe(2);

    // v1: baseline, no delta on either column.
    expect(rows[0].textContent).toContain('v1');
    expect(rows[0].textContent).toContain('0.500');
    expect(rows[0].textContent).not.toContain('(+');

    // v2: judge 0.9 (+0.400), composite 0.8 (+0.300).
    expect(rows[1].textContent).toContain('0.900 (+0.400)');
    const compositeCell = rows[1].querySelector('[data-testid="composite-cell"]');
    expect(compositeCell!.textContent).toContain('0.800 (+0.300)');
  });

  it('renders a dash where a version lacks a scorer value', () => {
    const series: TrendSeries[] = [
      {
        scorer: { identity: 'j', kind: 'LlmJudge', judgeModel: 'claude-opus-4-8' },
        points: [pt(2, 0.9)], // only v2 has this scorer
      },
    ];
    // Composite exists for both versions so both rows appear.
    const el = render(series, [comp(1, 0.4), comp(2, 0.8)]);
    const rows = el.querySelectorAll('[data-testid="change-row"]');
    expect(rows.length).toBe(2);
    // v1 has no judge value → dash; and no delta carries into v2 from a missing prior.
    expect(rows[0].textContent).toContain('—');
    expect(rows[1].textContent).toContain('0.900');
    expect(rows[1].textContent).not.toContain('0.900 (+'); // no prior → no delta
  });
});
