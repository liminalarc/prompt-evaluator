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
          costUsd: 0.001,
          scores: [
            { scorerKind: 'Regex', scorerIdentity: 'a', judgeModel: null, value: 1, passed: true, detail: null },
            { scorerKind: 'LlmJudge', scorerIdentity: 'b', judgeModel: 'claude-opus-4-8', value: 0.9, passed: true, detail: 'judged' },
          ],
        },
      ],
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const fixtureRun = el.querySelector('[data-testid="fixture-run"]');
    expect(fixtureRun).toBeTruthy();
    expect(fixtureRun!.textContent).toContain('OUT:hello');

    const rows = el.querySelectorAll('[data-testid="scores"] tbody tr');
    expect(rows.length).toBe(2);
    expect(el.querySelector('[data-scorer="LlmJudge"]')!.textContent).toContain('claude-opus-4-8');
  });

  it('shows an error when the run cannot be loaded', () => {
    const fixture = TestBed.createComponent(EvalRunDetail);
    fixture.detectChanges();

    httpMock.expectOne('/api/eval-runs/run-1').flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="error"]')).toBeTruthy();
  });
});
