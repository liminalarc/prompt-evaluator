import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { Dataset } from '../dataset';
import { DatasetDetail } from './dataset-detail';
import { DatasetsApiService } from './datasets-api.service';
import { EvalRunsApiService } from '../eval-runs/eval-runs-api.service';
import { ModelsApiService } from '../models/models-api.service';
import { PromptsApiService } from '../prompts/prompts-api.service';

const dataset: Dataset = {
  id: 'abc',
  promptId: 'p1',
  name: 'Summaries',
  description: null,
  fixtures: [
    {
      id: 'f1',
      origin: 'Captured',
      label: null,
      description: null,
      input: 'captured input',
      upstreamContext: null,
      expectedOutput: null,
      seedFixtureId: null,
      createdAt: '2026-07-12T00:00:00Z',
    },
    {
      id: 'f2',
      origin: 'Synthetic',
      label: 'edge: empty',
      description: null,
      input: 'synthetic input',
      upstreamContext: null,
      expectedOutput: null,
      seedFixtureId: 'f1',
      createdAt: '2026-07-12T00:01:00Z',
    },
  ],
};

// Catalog with judge-capable models across both providers (1.13).
const models = [
  {
    id: 'm1',
    modelId: 'claude-opus-4-8',
    displayName: 'Claude Opus 4.8',
    provider: 'Anthropic',
    roles: ['subject', 'judge', 'generator'],
    inputPricePerMTokUsd: 5,
    outputPricePerMTokUsd: 25,
    isActive: true,
    available: true,
  },
  {
    id: 'm2',
    modelId: 'gpt-4o',
    displayName: 'GPT-4o',
    provider: 'OpenAi',
    roles: ['subject', 'judge'],
    inputPricePerMTokUsd: null,
    outputPricePerMTokUsd: null,
    isActive: true,
    available: true,
  },
];

describe('DatasetDetail origin filter', () => {
  function render() {
    TestBed.configureTestingModule({
      imports: [DatasetDetail],
      providers: [
        { provide: DatasetsApiService, useValue: { getDataset: () => of(dataset) } },
        {
          provide: EvalRunsApiService,
          useValue: { listScorers: () => of([]), listRuns: () => of([]) },
        },
        { provide: ModelsApiService, useValue: { listModels: () => of(models) } },
        {
          provide: PromptsApiService,
          useValue: {
            listPrompts: () => of([]),
            getPrompt: () =>
              of({ id: 'p1', folderId: null, name: 'Owner', description: null, versions: [] }),
          },
        },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'abc' } } } },
        provideRouter([]),
      ],
    });
    const fixture = TestBed.createComponent(DatasetDetail);
    fixture.detectChanges();
    return fixture;
  }

  function rowOrigins(fixture: ReturnType<typeof render>): string[] {
    return Array.from(
      fixture.nativeElement.querySelectorAll('[data-testid="fixtures"] tbody tr'),
    ).map((r) => (r as HTMLElement).getAttribute('data-origin')!);
  }

  it('shows all fixtures by default', () => {
    const fixture = render();
    expect(rowOrigins(fixture)).toEqual(['Captured', 'Synthetic']);
  });

  it('filters to a single origin', () => {
    const fixture = render();
    const select = fixture.nativeElement.querySelector(
      '[data-testid="origin-filter"]',
    ) as HTMLSelectElement;

    select.value = 'Synthetic';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(rowOrigins(fixture)).toEqual(['Synthetic']);
  });

  it('sources the judge-model dropdown from the catalog across providers', () => {
    const fixture = render();
    const cmp = fixture.componentInstance as unknown as {
      showAddScorer: { set: (v: boolean) => void };
      scorerKind: { set: (v: string) => void };
    };
    cmp.showAddScorer.set(true);
    cmp.scorerKind.set('LlmJudge');
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector(
      '[data-testid="judge-model"]',
    ) as HTMLSelectElement;
    const values = Array.from(select.options).map((o) => o.value);
    expect(values).toContain('claude-opus-4-8');
    expect(values).toContain('gpt-4o'); // an OpenAI judge is now selectable
  });
});

describe('DatasetDetail run form + scorer config', () => {
  const owningPrompt = {
    id: 'p1',
    folderId: null,
    name: 'Summarizer',
    description: null,
    versions: [
      {
        id: 'v1',
        versionNumber: 1,
        content: 'You summarize.',
        targetModel: 'claude-opus-4-8',
        label: null,
        sourceApp: null,
        createdAt: '2026-07-12T00:00:00Z',
      },
    ],
  };

  function render(overrides: { triggerRun?: () => unknown } = {}) {
    TestBed.configureTestingModule({
      imports: [DatasetDetail],
      providers: [
        { provide: DatasetsApiService, useValue: { getDataset: () => of(dataset) } },
        {
          provide: EvalRunsApiService,
          useValue: {
            listScorers: () => of([]),
            listRuns: () => of([]),
            triggerRun: overrides.triggerRun ?? (() => of({ id: 'run1' })),
            configureScorer: () => of({}),
          },
        },
        { provide: ModelsApiService, useValue: { listModels: () => of(models) } },
        {
          provide: PromptsApiService,
          useValue: { getPrompt: () => of(owningPrompt) },
        },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'abc' } } } },
        provideRouter([]),
      ],
    });
    const fixture = TestBed.createComponent(DatasetDetail);
    fixture.detectChanges();
    return fixture;
  }

  it('fixes the run form to the dataset owning prompt (no cross-org prompt picker) [B3]', () => {
    const fixture = render();
    // The free prompt dropdown is gone; the owning prompt is shown fixed and its versions load.
    expect(fixture.nativeElement.querySelector('[data-testid="prompt-select"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="run-prompt"]').textContent).toContain(
      'Summarizer',
    );
    const versionSelect = fixture.nativeElement.querySelector(
      '[data-testid="version-select"]',
    ) as HTMLSelectElement;
    expect(Array.from(versionSelect.options).length).toBe(1);
  });

  it('renders the scorer rubric/config as a multi-line textarea [B4]', () => {
    const fixture = render();
    const cmp = fixture.componentInstance as unknown as {
      showAddScorer: { set: (v: boolean) => void };
    };
    cmp.showAddScorer.set(true);
    fixture.detectChanges();
    const field = fixture.nativeElement.querySelector('[data-testid="scorer-config"]');
    expect(field.tagName).toBe('TEXTAREA');
  });

  it('disables Add scorer until required config is supplied for Regex/JsonSchema [B5]', () => {
    const fixture = render();
    const cmp = fixture.componentInstance as unknown as {
      showAddScorer: { set: (v: boolean) => void };
      scorerKind: { set: (v: string) => void };
      scorerConfig: { set: (v: string) => void };
    };
    cmp.showAddScorer.set(true);
    cmp.scorerKind.set('Regex');
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector(
      '[data-testid="add-scorer"]',
    ) as HTMLButtonElement;
    expect(button.disabled).toBe(true);

    cmp.scorerConfig.set('^OUT:');
    fixture.detectChanges();
    expect(button.disabled).toBe(false);
  });

  it('shows the fixture label column [U7]', () => {
    const fixture = render();
    const table = fixture.nativeElement.querySelector('[data-testid="fixtures"]') as HTMLElement;
    expect(table.querySelector('thead')!.textContent).toContain('Label');
    expect(table.textContent).toContain('edge: empty'); // f2's label
  });

  it('adds a manual fixture with origin, label and description [U6/U7/U8]', () => {
    let captured: unknown = null;
    const fixture = render();
    const api = TestBed.inject(DatasetsApiService) as unknown as {
      captureFixtures: (id: string, t: unknown) => unknown;
    };
    api.captureFixtures = (_id: string, tuples: unknown) => {
      captured = tuples;
      return of(dataset);
    };
    const cmp = fixture.componentInstance as unknown as {
      showCapture: { set: (v: boolean) => void };
      promptInput: { set: (v: string) => void };
      fixtureLabel: { set: (v: string) => void };
      fixtureOrigin: { set: (v: string) => void };
      capture: (e: Event) => void;
    };
    cmp.showCapture.set(true);
    fixture.detectChanges();
    cmp.promptInput.set('hand-written case');
    cmp.fixtureLabel.set('empty thread');
    cmp.fixtureOrigin.set('Synthetic');
    cmp.capture(new Event('submit'));

    expect(captured).toEqual([
      jasmine.objectContaining({
        promptInput: 'hand-written case',
        origin: 'Synthetic',
        label: 'empty thread',
      }),
    ]);
  });

  // 2.11 — Cancel on the reveal/expand surfaces (add-fixture, generate, edit-fixture, scorers).
  it('cancels the add-fixture form, discarding input and collapsing it without a capture [2.11]', () => {
    let captureCalled = false;
    const fixture = render();
    const api = TestBed.inject(DatasetsApiService) as unknown as {
      captureFixtures: (id: string, t: unknown) => unknown;
    };
    api.captureFixtures = () => {
      captureCalled = true;
      return of(dataset);
    };
    const cmp = fixture.componentInstance as unknown as {
      showCapture: { set: (v: boolean) => void } & (() => boolean);
      promptInput: { set: (v: string) => void } & (() => string);
      fixtureLabel: { set: (v: string) => void } & (() => string);
      cancelCapture: () => void;
    };
    cmp.showCapture.set(true);
    fixture.detectChanges();
    // Cancel paired with the submit.
    expect(fixture.nativeElement.querySelector('[data-testid="cancel-capture"]')).not.toBeNull();

    cmp.promptInput.set('half-typed case');
    cmp.fixtureLabel.set('draft');
    cmp.cancelCapture();
    fixture.detectChanges();

    expect(cmp.showCapture()).toBe(false); // collapsed
    expect(cmp.promptInput()).toBe(''); // input discarded
    expect(cmp.fixtureLabel()).toBe('');
    expect(captureCalled).toBe(false); // nothing persisted
    expect(fixture.nativeElement.querySelector('[data-testid="capture"]')).toBeNull();
  });

  it('cancels an open fixture-metadata editor, collapsing the row [2.11]', () => {
    const fixture = render();
    const cmp = fixture.componentInstance as unknown as {
      toggleFixture: (id: string) => void;
      cancelEditFixture: () => void;
      expandedFixtureId: () => string | null;
    };
    cmp.toggleFixture('f1');
    fixture.detectChanges();
    expect(cmp.expandedFixtureId()).toBe('f1');
    expect(
      fixture.nativeElement.querySelector('[data-testid="cancel-edit-fixture"]'),
    ).not.toBeNull();

    cmp.cancelEditFixture();
    fixture.detectChanges();
    expect(cmp.expandedFixtureId()).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="fixture-detail"]')).toBeNull();
  });

  it('expands a fixture row and edits its label via PATCH [U6/U7]', () => {
    let patched: { id: string; fixtureId: string; label: string | null } | null = null;
    const fixture = render();
    const api = TestBed.inject(DatasetsApiService) as unknown as {
      editFixture: (
        id: string,
        fixtureId: string,
        label: string | null,
        description: string | null,
      ) => unknown;
    };
    api.editFixture = (id, fixtureId, label) => {
      patched = { id, fixtureId, label };
      return of(dataset);
    };
    const cmp = fixture.componentInstance as unknown as {
      toggleFixture: (id: string) => void;
      editFixtureLabel: { set: (v: string) => void };
      saveFixtureMeta: (e: Event, id: string) => void;
    };
    cmp.toggleFixture('f1');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="fixture-detail"]')).not.toBeNull();

    cmp.editFixtureLabel.set('renamed');
    cmp.saveFixtureMeta(new Event('submit'), 'f1');
    expect(patched).toEqual(jasmine.objectContaining({ fixtureId: 'f1', label: 'renamed' }));
  });

  it('expands a scorer row to edit (reconfigure) or remove it [U9]', () => {
    let reconfigured: { scorerId: string; body: { config: string | null } } | null = null;
    const scorer = {
      id: 's1',
      kind: 'Regex',
      config: '^a',
      judgeModel: null,
      identity: 'h1',
      createdAt: '2026-07-12T00:00:00Z',
    };
    const fixture = render();
    const evalApi = TestBed.inject(EvalRunsApiService) as unknown as {
      listScorers: () => unknown;
      reconfigureScorer: (id: string, scorerId: string, body: unknown) => unknown;
    };
    evalApi.listScorers = () => of([scorer]);
    evalApi.reconfigureScorer = (_id: string, scorerId: string, body: unknown) => {
      reconfigured = { scorerId, body: body as { config: string | null } };
      return of(scorer);
    };
    const cmp = fixture.componentInstance as unknown as {
      loadScorersForTest?: () => void;
      scorers: { set: (v: unknown[]) => void };
      toggleScorer: (s: unknown) => void;
      editScorerConfig: { set: (v: string) => void };
      saveScorer: (e: Event, id: string) => void;
    };
    cmp.scorers.set([scorer]);
    fixture.detectChanges();

    cmp.toggleScorer(scorer);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="scorer-detail"]')).not.toBeNull();

    cmp.editScorerConfig.set('^b');
    cmp.saveScorer(new Event('submit'), 's1');
    expect(reconfigured).toEqual(
      jasmine.objectContaining({
        scorerId: 's1',
        body: jasmine.objectContaining({ kind: 'Regex', config: '^b' }),
      }),
    );
  });

  it('shows version, model and scorers in the runs table [U14]', () => {
    const fixture = render();
    const cmp = fixture.componentInstance as unknown as {
      runs: { set: (v: unknown[]) => void };
    };
    cmp.runs.set([
      {
        id: 'run1',
        promptId: 'p1',
        promptVersionId: 'v1',
        createdAt: '2026-07-12T00:00:00Z',
        fixtureCount: 3,
        scoreCount: 6,
        scorerKinds: ['LlmJudge', 'Regex'],
      },
    ]);
    fixture.detectChanges();
    const runsTable = fixture.nativeElement.querySelector('[data-testid="runs"]') as HTMLElement;
    expect(runsTable.textContent).toContain('v1'); // version (from owning prompt's versions)
    expect(runsTable.textContent).toContain('claude-opus-4-8'); // target model
    expect(runsTable.textContent).toContain('Regex'); // scorer kind chip
    expect(runsTable.textContent).toContain('LlmJudge');
  });

  it('surfaces the server error message when a run fails [B2]', () => {
    const fixture = render({
      triggerRun: () =>
        throwError(
          () =>
            new HttpErrorResponse({
              status: 502,
              error: { error: 'eval-runner: Anthropic not configured (missing credentials?).' },
            }),
        ),
    });
    const cmp = fixture.componentInstance as unknown as { triggerRun: (e: Event) => void };
    cmp.triggerRun(new Event('submit'));
    fixture.detectChanges();
    const banner = fixture.nativeElement.querySelector('[data-testid="error"]');
    expect(banner.textContent).toContain('eval-runner: Anthropic not configured');
  });

  it('shows a loud banner on a timeout (no structured {error} body) [R2]', () => {
    const fixture = render({
      // A gateway/timeout 5xx returns a non-JSON body — the structured-error path finds nothing.
      triggerRun: () => throwError(() => new HttpErrorResponse({ status: 504, error: null })),
    });
    const cmp = fixture.componentInstance as unknown as { triggerRun: (e: Event) => void };
    cmp.triggerRun(new Event('submit'));
    fixture.detectChanges();
    const banner = fixture.nativeElement.querySelector('[data-testid="error"]');
    expect(banner).not.toBeNull(); // never a silent no-op
    expect(banner.textContent).toContain('timed out');
  });

  it('shows a loud banner on a non-JSON gateway 5xx [R2]', () => {
    const fixture = render({
      triggerRun: () =>
        throwError(
          () => new HttpErrorResponse({ status: 502, error: '<html>502 Bad Gateway</html>' }),
        ),
    });
    const cmp = fixture.componentInstance as unknown as { triggerRun: (e: Event) => void };
    cmp.triggerRun(new Event('submit'));
    fixture.detectChanges();
    const banner = fixture.nativeElement.querySelector('[data-testid="error"]');
    expect(banner).not.toBeNull();
    expect(banner.textContent).toContain('502');
  });
});
