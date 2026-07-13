import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AnalyticsDashboard } from './analytics-dashboard';
import { RegressionFlag, TrendSeries } from '../analytics';

describe('AnalyticsDashboard', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AnalyticsDashboard],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function createAndLoadLists() {
    const fixture = TestBed.createComponent(AnalyticsDashboard);
    fixture.detectChanges(); // ngOnInit → loads prompts + datasets
    http.expectOne('/api/prompts').flush([
      {
        id: 'p1',
        name: 'Summarizer',
        description: null,
        versionCount: 2,
        latestTargetModel: 'claude-opus-4-8',
      },
    ]);
    http.expectOne('/api/datasets').flush([
      {
        id: 'd1',
        name: 'Summaries',
        description: null,
        fixtureCount: 4,
        capturedCount: 4,
        syntheticCount: 0,
      },
    ]);
    fixture.detectChanges();
    return fixture;
  }

  it('prompts the user to choose before a prompt + dataset are selected', () => {
    const fixture = createAndLoadLists();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="prompt-choose"]')).toBeTruthy();
  });

  it('loads trends and regressions once a prompt and dataset are selected', () => {
    const fixture = createAndLoadLists();
    const cmp = fixture.componentInstance as unknown as {
      promptId: { set(v: string): void };
      datasetId: { set(v: string): void };
    };
    cmp.promptId.set('p1');
    cmp.datasetId.set('d1');
    fixture.detectChanges(); // effect fires → trends + regressions requests

    const trends: TrendSeries[] = [
      {
        scorer: { identity: 'abc', kind: 'LlmJudge', judgeModel: 'claude-opus-4-8' },
        points: [
          {
            promptVersionId: 'v1',
            versionNumber: 1,
            versionLabel: null,
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
    const flags: RegressionFlag[] = [
      {
        scorer: { identity: 'abc', kind: 'LlmJudge', judgeModel: 'claude-opus-4-8' },
        fromVersionId: 'v1',
        fromVersionNumber: 1,
        fromVersionLabel: null,
        toVersionId: 'v2',
        toVersionNumber: 2,
        toVersionLabel: null,
        priorMean: 0.9,
        currentMean: 0.5,
        delta: -0.4,
        pValue: 0.001,
        pairedFixtureCount: 4,
      },
    ];

    http.expectOne((r) => r.url === '/api/analytics/trends').flush(trends);
    http.expectOne((r) => r.url === '/api/analytics/regressions').flush(flags);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="trend-chart"]')).toBeTruthy();
    const rows = el.querySelectorAll('[data-testid="regression-row"]');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('LlmJudge');
    expect(rows[0].textContent).toContain('v1 → v2');
  });

  it('shows a clean state when there are no regressions', () => {
    const fixture = createAndLoadLists();
    const cmp = fixture.componentInstance as unknown as {
      promptId: { set(v: string): void };
      datasetId: { set(v: string): void };
    };
    cmp.promptId.set('p1');
    cmp.datasetId.set('d1');
    fixture.detectChanges();

    http.expectOne((r) => r.url === '/api/analytics/trends').flush([]);
    http.expectOne((r) => r.url === '/api/analytics/regressions').flush([]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="no-regressions"]')).toBeTruthy();
  });

  it('loads a version comparison after selecting a prompt (defaults to the two latest versions)', () => {
    const fixture = createAndLoadLists();
    const cmp = fixture.componentInstance as unknown as {
      datasetId: { set(v: string): void };
      selectPrompt(id: string): void;
    };
    cmp.datasetId.set('d1');
    cmp.selectPrompt('p1');
    fixture.detectChanges();

    // selectPrompt fetches the full prompt (2 versions) → auto-selects from=v1, to=v2.
    http.expectOne('/api/prompts/p1').flush({
      id: 'p1',
      name: 'Summarizer',
      description: null,
      versions: [
        {
          id: 'v1',
          versionNumber: 1,
          content: '',
          targetModel: 'm',
          label: null,
          sourceApp: null,
          createdAt: '',
        },
        {
          id: 'v2',
          versionNumber: 2,
          content: '',
          targetModel: 'm',
          label: null,
          sourceApp: null,
          createdAt: '',
        },
      ],
    });
    fixture.detectChanges();

    http.expectOne((r) => r.url === '/api/analytics/trends').flush([]);
    http.expectOne((r) => r.url === '/api/analytics/regressions').flush([]);
    const comparisonReq = http.expectOne((r) => r.url === '/api/analytics/comparison');
    expect(comparisonReq.request.params.get('fromVersionId')).toBe('v1');
    expect(comparisonReq.request.params.get('toVersionId')).toBe('v2');
    comparisonReq.flush({
      fromVersionId: 'v1',
      fromVersionNumber: 1,
      fromVersionLabel: null,
      fromRunId: 'r1',
      toVersionId: 'v2',
      toVersionNumber: 2,
      toVersionLabel: null,
      toRunId: 'r2',
      scorers: [
        {
          scorer: { identity: 'abc', kind: 'FuzzyMatch', judgeModel: null },
          fromMean: 0.7,
          toMean: 0.5,
          delta: -0.2,
          fixtures: [
            {
              fixtureId: 'aaaaaaaa-0000-0000-0000-000000000000',
              fromValue: 0.7,
              toValue: 0.5,
              delta: -0.2,
            },
          ],
        },
      ],
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="from-version"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="scorer-comparison"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="fixture-delta-row"]')).toBeTruthy();
  });
});
