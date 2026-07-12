import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Dataset } from '../dataset';
import { DatasetDetail } from './dataset-detail';
import { DatasetsApiService } from './datasets-api.service';
import { EvalRunsApiService } from '../eval-runs/eval-runs-api.service';
import { PromptsApiService } from '../prompts/prompts-api.service';

const dataset: Dataset = {
  id: 'abc',
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
        { provide: PromptsApiService, useValue: { listPrompts: () => of([]) } },
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
});
