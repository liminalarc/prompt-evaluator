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
      input: 'captured input',
      upstreamContext: null,
      expectedOutput: null,
      seedFixtureId: null,
      createdAt: '2026-07-12T00:00:00Z',
    },
    {
      id: 'f2',
      origin: 'Synthetic',
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
});
