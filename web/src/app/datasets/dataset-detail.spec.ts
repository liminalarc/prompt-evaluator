import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
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
