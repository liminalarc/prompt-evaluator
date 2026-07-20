import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpRequest } from '@angular/common/http';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AiUsageAdmin } from './ai-usage-admin';
import { AiUsageMetrics, AiUsageCallsPage } from './ai-usage';

const metrics: AiUsageMetrics = {
  totalCostUsd: 12.34,
  inputTokens: 1000,
  outputTokens: 500,
  cacheCreationTokens: 0,
  cacheReadTokens: 0,
  callCount: 3,
  avgCostPerCall: 4.11,
  avgTokensPerCall: 500,
  successRate: 0.6667,
  latencyP50Ms: 120,
  latencyP95Ms: 300,
};

const callsPage: AiUsageCallsPage = {
  items: [
    {
      id: 'c1',
      occurredAt: '2026-07-18T10:00:00Z',
      feature: 'LlmJudge',
      model: 'claude-opus-4-8',
      inputTokens: 100,
      outputTokens: 50,
      cacheCreationTokens: 0,
      cacheReadTokens: 0,
      costUsd: 0.5,
      organizationId: null,
      userId: null,
      status: 'Success',
      latencyMs: 90,
      requestId: 'req_123',
    },
  ],
  page: 1,
  pageSize: 25,
  totalCount: 1,
};

describe('AiUsageAdmin (admin AI usage)', () => {
  let httpMock: HttpTestingController;

  const isPath = (path: string) => (r: HttpRequest<unknown>) => r.url === path;
  const base = '/api/admin/ai-usage';

  function setup() {
    TestBed.configureTestingModule({
      imports: [AiUsageAdmin],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
      ],
    });
    const fixture = TestBed.createComponent(AiUsageAdmin);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → lookups + reload
    flushLookups();
    flushDashboard();
    fixture.detectChanges();
    return fixture;
  }

  function flushLookups() {
    httpMock.expectOne('/api/models?includeInactive=true').flush([]);
    httpMock.expectOne('/api/admin/users').flush([]);
    httpMock.expectOne('/api/admin/organizations').flush([]);
  }

  // Flushes one full dashboard reload batch (summary + timeseries + 4 breakdowns + calls + budgets).
  function flushDashboard() {
    httpMock.expectOne(isPath(`${base}/summary`)).flush(metrics);
    httpMock.expectOne(isPath(`${base}/timeseries`)).flush([]);
    httpMock.match(isPath(`${base}/breakdown`)).forEach((r) => r.flush([]));
    httpMock.expectOne(isPath(`${base}/calls`)).flush(callsPage);
    httpMock.expectOne(isPath(`${base}/budgets/status`)).flush([]);
  }

  afterEach(() => httpMock.verify());

  it('renders the dashboard, filter bar, and calls table', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="filter-bar"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="summary-tiles"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="tile-total-cost"]')?.textContent).toContain('12.34');
    expect(el.querySelector('[data-testid="calls-table"]')?.textContent).toContain(
      'claude-opus-4-8',
    );
    expect(el.querySelector('[data-testid="calls-table"]')?.textContent).toContain('req_123');
  });

  it('drives every query from the applied filter', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleFeature: (f: string, on: boolean) => void;
      draftFrom: { set: (v: string) => void };
      applyFilters: () => void;
    };
    cmp.toggleFeature('SubjectExecution', true);
    cmp.draftFrom.set('2026-07-01');
    cmp.applyFilters();

    // A fresh batch fires; assert the filter is serialized onto the requests.
    const summary = httpMock.expectOne(isPath(`${base}/summary`));
    expect(summary.request.params.get('features')).toBe('SubjectExecution');
    expect(summary.request.params.get('from')).toBe('2026-07-01');
    summary.flush(metrics);

    const calls = httpMock.expectOne(isPath(`${base}/calls`));
    expect(calls.request.params.get('features')).toBe('SubjectExecution');
    calls.flush(callsPage);

    httpMock.expectOne(isPath(`${base}/timeseries`)).flush([]);
    httpMock.match(isPath(`${base}/breakdown`)).forEach((r) => r.flush([]));
    httpMock.expectOne(isPath(`${base}/budgets/status`)).flush([]);
  });

  it('exports the current filtered view as CSV', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as { exportCsv: () => void };
    cmp.exportCsv();
    const req = httpMock.expectOne(isPath(`${base}/export.csv`));
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('text');
    req.flush('header\nrow');
  });

  it('creates a budget, posting the body, then reloads the status list', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      startCreateBudget: () => void;
      bLimit: { set: (v: string) => void };
      saveBudget: (e: Event) => void;
    };
    cmp.startCreateBudget();
    cmp.bLimit.set('100');
    cmp.saveBudget(new Event('submit'));

    const post = httpMock.expectOne(`${base}/budgets`);
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({
      scope: 'Global',
      scopeValue: null,
      limitUsd: 100,
      alertThresholdPercent: 80,
    });
    post.flush({
      id: 'b1',
      scope: 'Global',
      scopeValue: null,
      limitUsd: 100,
      period: 'Monthly',
      alertThresholdPercent: 80,
      createdAt: '2026-07-19T00:00:00Z',
    });
    httpMock.expectOne(isPath(`${base}/budgets/status`)).flush([]); // reload
  });

  it('paginates the calls table', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      nextPage: () => void;
      page: () => number;
      totalPages: () => number;
    };
    // With totalCount 1 / pageSize 25 there is a single page — next is a no-op (guarded).
    expect(cmp.totalPages()).toBe(1);
    cmp.nextPage();
    expect(cmp.page()).toBe(1);
  });
});
