import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { EvalRunDetail } from './eval-run-detail';

describe('EvalRunDetail', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EvalRunDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'run-1' }) } },
        },
      ],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('fetches the run and renders per-fixture scores', () => {
    const fixture = TestBed.createComponent(EvalRunDetail);
    fixture.detectChanges();

    const req = httpMock.expectOne('/api/eval-runs/run-1');
    expect(req.request.method).toBe('GET');
    req.flush({
      id: 'run-1',
      promptId: 'p1',
      promptVersionId: 'v1',
      datasetId: 'd1',
      createdAt: '2026-07-12T12:00:00Z',
      results: [
        {
          fixtureId: 'f1',
          modelOutput: 'OUT:hello',
          latencyMs: 100,
          inputTokens: 1000,
          outputTokens: 500,
          costUsd: 0.001,
          scores: [
            {
              scorerKind: 'Regex',
              scorerIdentity: 'a',
              judgeModel: null,
              value: 1,
              passed: true,
              detail: null,
            },
            {
              scorerKind: 'LlmJudge',
              scorerIdentity: 'b',
              judgeModel: 'claude-opus-4-8',
              value: 0.9,
              passed: true,
              detail: 'judged',
            },
          ],
        },
      ],
    });
    // The run links back to both its prompt and its dataset — names fetched for the trail.
    httpMock
      .expectOne('/api/prompts/p1')
      .flush({ id: 'p1', folderId: null, name: 'Summarizer', description: null, versions: [] });
    httpMock.expectOne('/api/datasets/d1').flush({
      id: 'd1',
      promptId: 'p1',
      name: 'Summaries',
      description: null,
      fixtures: [],
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const fixtureRun = el.querySelector('[data-testid="fixture-run"]');
    expect(fixtureRun).toBeTruthy();
    // Collapsed by default (U10): the summary row shows headline scores; detail is hidden.
    expect(el.querySelector('[data-testid="fixture-run-detail"]')).toBeNull();
    expect(fixtureRun!.textContent).toContain('Regex'); // headline score badge in the summary

    // Expand to reveal the full output + scores table.
    (el.querySelector('[data-testid="fixture-run-summary"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixtureRun!.textContent).toContain('OUT:hello');

    const rows = el.querySelectorAll('[data-testid="scores"] tbody tr');
    expect(rows.length).toBe(2);
    expect(el.querySelector('[data-scorer="LlmJudge"]')!.textContent).toContain('claude-opus-4-8');

    // Per-fixture input/output token counts are surfaced (2.5).
    expect(el.querySelector('[data-testid="input-tokens"]')!.textContent).toContain('1000');
    expect(el.querySelector('[data-testid="output-tokens"]')!.textContent).toContain('500');

    // Back-links to the dataset and the prompt are both present.
    const hrefs = Array.from(el.querySelectorAll('a')).map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('/prompts/p1');
    expect(hrefs).toContain('/datasets/d1');
  });

  // U21 (2.23) — scorer badges render in a stable order on every row, even when the API returns
  // each fixture's scores in a different order, so the columns line up for scanning.
  it('renders scorer badges in the same stable order on every test-case row [U21]', () => {
    const fixture = TestBed.createComponent(EvalRunDetail);
    fixture.detectChanges();

    const regex = {
      scorerKind: 'Regex',
      scorerIdentity: 'a',
      judgeModel: null,
      value: 1,
      passed: true,
      detail: null,
    };
    const judge = {
      scorerKind: 'LlmJudge',
      scorerIdentity: 'b',
      judgeModel: 'claude-opus-4-8',
      value: 0.9,
      passed: true,
      detail: 'judged',
    };
    httpMock.expectOne('/api/eval-runs/run-1').flush({
      id: 'run-1',
      promptId: 'p1',
      promptVersionId: 'v1',
      datasetId: 'd1',
      createdAt: '2026-07-12T12:00:00Z',
      results: [
        // f1: Regex then LlmJudge …
        { fixtureId: 'f1', modelOutput: 'x', latencyMs: 1, inputTokens: 1, outputTokens: 1, costUsd: 0, scores: [regex, judge] },
        // f2: LlmJudge then Regex — the reverse order from the API.
        { fixtureId: 'f2', modelOutput: 'y', latencyMs: 1, inputTokens: 1, outputTokens: 1, costUsd: 0, scores: [judge, regex] },
      ],
    });
    httpMock
      .expectOne('/api/prompts/p1')
      .flush({ id: 'p1', folderId: null, name: 'Summarizer', description: null, versions: [] });
    httpMock
      .expectOne('/api/datasets/d1')
      .flush({ id: 'd1', promptId: 'p1', name: 'Summaries', description: null, fixtures: [] });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const rows = el.querySelectorAll('[data-testid="fixture-run"]');
    expect(rows.length).toBe(2);
    const kindsPerRow = Array.from(rows).map((r) =>
      Array.from(r.querySelectorAll('.fixture-run__scores app-status-badge')).map(
        (b) => b.textContent!.trim().split(' ')[0],
      ),
    );
    // Both rows show the same order despite the API's differing input order.
    expect(kindsPerRow[0]).toEqual(kindsPerRow[1]);
    expect(kindsPerRow[0]).toEqual(['LlmJudge', 'Regex']); // stable sort by kind
  });

  it('shows an error when the run cannot be loaded', () => {
    const fixture = TestBed.createComponent(EvalRunDetail);
    fixture.detectChanges();

    httpMock
      .expectOne('/api/eval-runs/run-1')
      .flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="error"]'),
    ).toBeTruthy();
  });
});
