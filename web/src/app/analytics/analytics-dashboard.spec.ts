import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AnalyticsDashboard } from './analytics-dashboard';
import { RegressionFlag, TrendSeries } from '../analytics';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { OrgContextStore } from '../shared/org-context.store';

describe('AnalyticsDashboard', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [AnalyticsDashboard],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: OrganizationsApiService,
          useValue: { listOrganizations: () => of([{ id: 'o1', name: 'Acme' }]) },
        },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(OrgContextStore).load(); // resolves global org context → o1
  }, 10000);

  afterEach(() => http.verify());

  // The dashboard scopes its lists to the active org: the org's prompts + the datasets that
  // belong to them (intersected by prompt id).
  function createAndLoadLists() {
    const fixture = TestBed.createComponent(AnalyticsDashboard);
    fixture.detectChanges(); // org effect → loads the org's prompts + datasets
    http.expectOne('/api/organizations/o1/prompts').flush([
      {
        id: 'p1',
        folderId: null,
        name: 'Summarizer',
        description: null,
        versionCount: 2,
        latestTargetModel: 'claude-opus-4-8',
      },
    ]);
    http.expectOne('/api/datasets').flush([
      {
        id: 'd1',
        promptId: 'p1',
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

  // B8: the dataset picker must show only the selected prompt's datasets, not every org dataset.
  function createWithTwoPrompts() {
    const fixture = TestBed.createComponent(AnalyticsDashboard);
    fixture.detectChanges();
    http.expectOne('/api/organizations/o1/prompts').flush([
      {
        id: 'p1',
        folderId: null,
        name: 'Summarizer',
        description: null,
        versionCount: 2,
        latestTargetModel: 'claude-opus-4-8',
      },
      {
        id: 'p2',
        folderId: null,
        name: 'Debrief',
        description: null,
        versionCount: 1,
        latestTargetModel: 'claude-sonnet-4-6',
      },
    ]);
    http.expectOne('/api/datasets').flush([
      {
        id: 'd1',
        promptId: 'p1',
        name: 'Summaries',
        description: null,
        fixtureCount: 4,
        capturedCount: 4,
        syntheticCount: 0,
      },
      {
        id: 'd2',
        promptId: 'p2',
        name: 'Core round scenarios',
        description: null,
        fixtureCount: 4,
        capturedCount: 0,
        syntheticCount: 4,
      },
    ]);
    fixture.detectChanges();
    return fixture;
  }

  it('scopes the dataset picker to the selected prompt (no cross-prompt leak) [B8]', () => {
    const fixture = createWithTwoPrompts();
    const cmp = fixture.componentInstance as unknown as { selectPrompt(id: string): void };

    cmp.selectPrompt('p1');
    fixture.detectChanges();
    http.expectOne('/api/prompts/p1').flush({
      id: 'p1',
      name: 'Summarizer',
      description: null,
      versions: [],
    });
    fixture.detectChanges();

    const options = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll(
        '[data-testid="dataset-select"] option',
      ),
    ).map((o) => o.textContent?.trim());
    // Only p1's dataset is offered; p2's dataset must not leak in.
    expect(options).toContain('Summaries');
    expect(options).not.toContain('Core round scenarios');
  });

  it('clears a carried-over foreign dataset when switching prompts [B8]', () => {
    const fixture = createWithTwoPrompts();
    const cmp = fixture.componentInstance as unknown as {
      datasetId: { set(v: string): void } & (() => string | null);
      selectPrompt(id: string): void;
    };
    // Pretend d2 (belongs to p2) was selected, then switch to p1.
    cmp.datasetId.set('d2');
    cmp.selectPrompt('p1');
    fixture.detectChanges();
    http.expectOne('/api/prompts/p1').flush({
      id: 'p1',
      name: 'Summarizer',
      description: null,
      versions: [],
    });
    // The foreign dataset selection is dropped (no analytics fetched for a mismatched pair).
    expect(cmp.datasetId()).toBeNull();
  });

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
        confidence: 'Confirmed',
      },
    ];

    http.expectOne((r) => r.url === '/api/analytics/trends').flush(trends);
    http.expectOne((r) => r.url === '/api/analytics/regressions').flush(flags);
    http.expectOne((r) => r.url === '/api/analytics/variance').flush([]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="trend-chart"]')).toBeTruthy();
    const rows = el.querySelectorAll('[data-testid="regression-row"]');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('LlmJudge');
    expect(rows[0].textContent).toContain('v1 → v2');
    // A confirmed flag is not shown in the muted "possible" treatment.
    expect(el.querySelector('[data-testid="regression-unverified"]')).toBeFalsy();
  });

  it('renders an unverified drop as a distinct "possible" regression, not the empty state', () => {
    const fixture = createAndLoadLists();
    const cmp = fixture.componentInstance as unknown as {
      promptId: { set(v: string): void };
      datasetId: { set(v: string): void };
    };
    cmp.promptId.set('p1');
    cmp.datasetId.set('d1');
    fixture.detectChanges();

    // A single-fixture 1.0 -> 0.0 drop: clears the threshold but n=1 → no significance.
    const flags: RegressionFlag[] = [
      {
        scorer: { identity: 'abc', kind: 'LlmJudge', judgeModel: 'claude-opus-4-8' },
        fromVersionId: 'v1',
        fromVersionNumber: 1,
        fromVersionLabel: null,
        toVersionId: 'v2',
        toVersionNumber: 2,
        toVersionLabel: null,
        priorMean: 1.0,
        currentMean: 0.0,
        delta: -1.0,
        pValue: null,
        pairedFixtureCount: 1,
        confidence: 'Unverified',
      },
    ];

    http.expectOne((r) => r.url === '/api/analytics/trends').flush([]);
    http.expectOne((r) => r.url === '/api/analytics/regressions').flush(flags);
    http.expectOne((r) => r.url === '/api/analytics/variance').flush([]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    // Not the misleading empty state, and not the confirmed table.
    expect(el.querySelector('[data-testid="no-regressions"]')).toBeFalsy();
    expect(el.querySelector('[data-testid="regressions"]')).toBeFalsy();

    const unverified = el.querySelector('[data-testid="regression-unverified"]');
    expect(unverified).toBeTruthy();
    expect(unverified!.textContent).toContain('Possible');
    const rows = el.querySelectorAll('[data-testid="regression-unverified-row"]');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('v1 → v2');
  });

  it('flags a cross-model comparison when the two versions ran on different subject models [R5]', () => {
    const fixture = createAndLoadLists();
    const cmp = fixture.componentInstance as unknown as {
      datasetId: { set(v: string): void };
      selectPrompt(id: string): void;
    };
    cmp.datasetId.set('d1');
    cmp.selectPrompt('p1');
    fixture.detectChanges();

    // v1 ran on Sonnet 4.6, v2 on Sonnet 5 — the classic round-debrief drift.
    http.expectOne('/api/prompts/p1').flush({
      id: 'p1',
      name: 'Summarizer',
      description: null,
      versions: [
        {
          id: 'v1',
          versionNumber: 1,
          content: '',
          targetModel: 'claude-sonnet-4-6',
          label: null,
          sourceApp: null,
          createdAt: '',
        },
        {
          id: 'v2',
          versionNumber: 2,
          content: '',
          targetModel: 'claude-sonnet-5',
          label: null,
          sourceApp: null,
          createdAt: '',
        },
      ],
    });
    fixture.detectChanges();

    http.expectOne((r) => r.url === '/api/analytics/trends').flush([]);
    http.expectOne((r) => r.url === '/api/analytics/regressions').flush([]);
    http.expectOne((r) => r.url === '/api/analytics/variance').flush([]);
    http
      .expectOne((r) => r.url === '/api/analytics/comparison')
      .flush({
        fromVersionId: 'v1',
        fromVersionNumber: 1,
        fromVersionLabel: null,
        fromRunId: 'r1',
        toVersionId: 'v2',
        toVersionNumber: 2,
        toVersionLabel: null,
        toRunId: 'r2',
        scorers: [],
      });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const warn = el.querySelector('[data-testid="cross-model-warning"]');
    expect(warn).toBeTruthy();
    expect(warn!.textContent).toContain('claude-sonnet-4-6');
    expect(warn!.textContent).toContain('claude-sonnet-5');
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
    http.expectOne((r) => r.url === '/api/analytics/variance').flush([]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="no-regressions"]')).toBeTruthy();
  });

  it('shows per-version mean ± spread in a Score stability card [2.14]', () => {
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
    http
      .expectOne((r) => r.url === '/api/analytics/variance')
      .flush([
        {
          scorer: { identity: 'abc', kind: 'LlmJudge', judgeModel: 'claude-opus-4-8' },
          versions: [
            {
              promptVersionId: 'v1',
              versionNumber: 1,
              versionLabel: null,
              runCount: 2,
              aggregate: { mean: 0.75, stdDev: 0.05, sampleCount: 2, min: 0.7, max: 0.8 },
              fixtures: [],
            },
          ],
        },
      ]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="variance"]')).toBeTruthy();
    const row = el.querySelector('[data-testid="variance-row"]')!;
    expect(row.textContent).toContain('v1');
    expect(row.textContent).toContain('2'); // run count
    expect(row.textContent).toContain('0.750 ± 0.050'); // mean ± spread
  });

  it('flags a version delta that is within run-to-run noise [2.14]', () => {
    const fixture = createAndLoadLists();
    const cmp = fixture.componentInstance as unknown as {
      datasetId: { set(v: string): void };
      selectPrompt(id: string): void;
    };
    cmp.datasetId.set('d1');
    cmp.selectPrompt('p1');
    fixture.detectChanges();

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
    // Both versions run 3× with a 0.15 spread each → combined spread 0.30; the compare Δ is -0.20,
    // which sits inside that noise band → the within-noise banner should show.
    http
      .expectOne((r) => r.url === '/api/analytics/variance')
      .flush([
        {
          scorer: { identity: 'abc', kind: 'FuzzyMatch', judgeModel: null },
          versions: [
            {
              promptVersionId: 'v1',
              versionNumber: 1,
              versionLabel: null,
              runCount: 3,
              aggregate: { mean: 0.7, stdDev: 0.15, sampleCount: 3, min: 0.5, max: 0.85 },
              fixtures: [],
            },
            {
              promptVersionId: 'v2',
              versionNumber: 2,
              versionLabel: null,
              runCount: 3,
              aggregate: { mean: 0.5, stdDev: 0.15, sampleCount: 3, min: 0.35, max: 0.65 },
              fixtures: [],
            },
          ],
        },
      ]);
    http
      .expectOne((r) => r.url === '/api/analytics/comparison')
      .flush({
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
            fixtures: [],
          },
        ],
      });
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="within-noise"]'),
    ).toBeTruthy();
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
    http.expectOne((r) => r.url === '/api/analytics/variance').flush([]);
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
              fixtureLabel: null,
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
