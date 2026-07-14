import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { PromptDetail } from './prompt-detail';

describe('PromptDetail (unified workspace)', () => {
  let httpMock: HttpTestingController;

  const prompt = { id: 'p1', folderId: null, name: 'Summarizer', description: null, versions: [] };
  const datasets = [
    {
      id: 'd1',
      promptId: 'p1',
      name: 'Summaries',
      description: null,
      fixtureCount: 3,
      capturedCount: 3,
      syntheticCount: 0,
    },
  ];

  function setup() {
    TestBed.configureTestingModule({
      imports: [PromptDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'p1' } } } },
      ],
    });
    const fixture = TestBed.createComponent(PromptDetail);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → load prompt + its datasets
    httpMock.expectOne('/api/prompts/p1').flush(prompt);
    httpMock.expectOne('/api/prompts/p1/datasets').flush(datasets);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('shows the prompt together with its datasets', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.page-header__title')?.textContent).toContain('Summarizer');
    expect(el.querySelector('[data-testid="datasets"]')?.textContent).toContain('Summaries');
  });

  it('creates a dataset under the prompt and reloads the list', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      datasetName: { set: (v: string) => void };
      createDataset: (e: Event) => void;
    };
    cmp.datasetName.set('New set');
    cmp.createDataset(new Event('submit'));

    const create = httpMock.expectOne('/api/prompts/p1/datasets');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'New set', description: null });
    create.flush({ id: 'd2', promptId: 'p1', name: 'New set', description: null, fixtures: [] });
    httpMock.expectOne('/api/prompts/p1/datasets').flush(datasets); // reload
  });

  it('loads analytics for a selected dataset', () => {
    const fixture = setup();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector(
      '[data-testid="analytics-dataset"]',
    );
    select.value = 'd1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const trends = httpMock.expectOne('/api/analytics/trends?promptId=p1&datasetId=d1');
    expect(trends.request.method).toBe('GET');
    trends.flush([]);
  });
});
