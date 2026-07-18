import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { PromptDetail } from './prompt-detail';

describe('PromptDetail (unified workspace)', () => {
  let httpMock: HttpTestingController;

  // One version carries a legacy free-text target model (not in the catalog) — it must still display.
  const prompt = {
    id: 'p1',
    folderId: null,
    name: 'Summarizer',
    description: null,
    versions: [
      {
        id: 'v1',
        versionNumber: 1,
        content: 'Summarize: {input}',
        targetModel: 'legacy-model-x',
        label: null,
        sourceApp: null,
        createdAt: '2026-07-12T00:00:00Z',
      },
    ],
  };
  const models = [
    {
      id: 'm1',
      modelId: 'claude-sonnet-5',
      displayName: 'Claude Sonnet 5',
      provider: 'Anthropic',
      roles: ['subject', 'judge', 'generator'],
      inputPricePerMTokUsd: 3,
      outputPricePerMTokUsd: 15,
      isActive: true,
      available: true,
    },
    {
      id: 'm2',
      modelId: 'gpt-4o-mini',
      displayName: 'GPT-4o mini',
      provider: 'OpenAi',
      roles: ['subject', 'judge'],
      inputPricePerMTokUsd: null,
      outputPricePerMTokUsd: null,
      isActive: true,
      available: true,
    },
    {
      id: 'm3',
      modelId: 'gpt-unconfigured',
      displayName: 'GPT (no key)',
      provider: 'OpenAi',
      roles: ['subject'],
      inputPricePerMTokUsd: null,
      outputPricePerMTokUsd: null,
      isActive: true,
      available: false,
    },
  ];
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
    httpMock.expectOne('/api/models').flush(models);
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

  it('sends a dataset description from the create form [U5]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      datasetName: { set: (v: string) => void };
      datasetDescription: { set: (v: string) => void };
      createDataset: (e: Event) => void;
    };
    cmp.datasetName.set('Described set');
    cmp.datasetDescription.set('captured golf rounds');
    cmp.createDataset(new Event('submit'));

    const create = httpMock.expectOne('/api/prompts/p1/datasets');
    expect(create.request.body).toEqual({
      name: 'Described set',
      description: 'captured golf rounds',
    });
    create.flush({
      id: 'd2',
      promptId: 'p1',
      name: 'Described set',
      description: 'captured golf rounds',
      fixtures: [],
    });
    httpMock.expectOne('/api/prompts/p1/datasets').flush(datasets); // reload
  });

  it('imports a text file into the add-version content signal', async () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      content: () => string;
      importVersionFile: (e: Event) => void;
    };

    const file = new File(['Summarize: {input}'], 'prompt.txt', { type: 'text/plain' });
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [file] });
    cmp.importVersionFile({ target: input } as unknown as Event);

    // FileReader.readAsText is async — poll the signal until the read lands.
    await new Promise<void>((resolve) => {
      const tick = () => (cmp.content() ? resolve() : setTimeout(tick, 5));
      tick();
    });
    expect(cmp.content()).toBe('Summarize: {input}');
  });

  it('rejects a non-text file with an error and leaves content empty', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      content: () => string;
      error: () => string | null;
      importVersionFile: (e: Event) => void;
    };

    const file = new File(['x'], 'logo.png', { type: 'image/png' });
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [file] });
    cmp.importVersionFile({ target: input } as unknown as Event);

    expect(cmp.error()).toContain('text file');
    expect(cmp.content()).toBe('');
  });

  it('offers target models from the catalog, including a non-Claude model (no free-text)', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      showAddVersion: { set: (v: boolean) => void };
    };
    cmp.showAddVersion.set(true);
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector(
      '[data-testid="target-model"]',
    ) as HTMLSelectElement;
    expect(select.tagName).toBe('SELECT'); // a droplist, not a free-text input
    const values = Array.from(select.options).map((o) => o.value);
    expect(values).toContain('claude-sonnet-5');
    expect(values).toContain('gpt-4o-mini');
  });

  it('marks a model whose provider is unavailable but keeps it selectable (eval-runner enforces)', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      showAddVersion: { set: (v: boolean) => void };
    };
    cmp.showAddVersion.set(true);
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector(
      '[data-testid="target-model"]',
    ) as HTMLSelectElement;
    const unavailable = Array.from(select.options).find((o) => o.value === 'gpt-unconfigured')!;
    expect(unavailable.textContent).toContain('unavailable'); // clearly marked
    expect(unavailable.disabled).toBe(false); // still selectable; the eval-runner returns a clear 400
  });

  it('displays a legacy free-text target model on an existing version (backward-compat)', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="versions"]')?.textContent).toContain('legacy-model-x');
  });

  it('expands a version row to reveal its content and an editable label [U2/U3]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as { toggleVersion: (id: string) => void };
    // Collapsed by default — no detail panel until a row is opened.
    expect(fixture.nativeElement.querySelector('[data-testid="version-detail"]')).toBeNull();

    cmp.toggleVersion('v1');
    fixture.detectChanges();
    const detail = fixture.nativeElement.querySelector('[data-testid="version-detail"]');
    expect(detail).not.toBeNull();
    expect(detail.textContent).toContain('Summarize: {input}'); // immutable content shown
    expect(detail.querySelector('[data-testid="edit-label"]')).not.toBeNull();
  });

  it('saves an edited version label via PATCH and reloads [U3]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleVersion: (id: string) => void;
      editLabel: { set: (v: string) => void };
      saveLabel: (e: Event, id: string) => void;
    };
    cmp.toggleVersion('v1');
    cmp.editLabel.set('renamed');
    cmp.saveLabel(new Event('submit'), 'v1');

    const patch = httpMock.expectOne('/api/prompts/p1/versions/v1');
    expect(patch.request.method).toBe('PATCH');
    expect(patch.request.body).toEqual({ label: 'renamed' });
    patch.flush({ ...prompt });
    httpMock.expectOne('/api/prompts/p1').flush(prompt); // reload
  });

  it('seeds the new-version form content from the latest version [U11]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleAddVersion: () => void;
      content: () => string;
    };
    expect(cmp.content()).toBe('');
    cmp.toggleAddVersion();
    expect(cmp.content()).toBe('Summarize: {input}'); // pre-filled from v1 to edit, not paste
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
