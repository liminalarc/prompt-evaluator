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
  // 1.16 — the derived per-version status loaded alongside the prompt (nothing current by default).
  const versionStatus = {
    promptId: 'p1',
    currentVersionId: null,
    backportTargetVersionId: null,
    versions: [
      {
        versionId: 'v1',
        versionNumber: 1,
        label: null,
        isCurrent: false,
        backportEligible: false,
        isBackportTarget: false,
        regressed: false,
      },
    ],
  };

  function setup() {
    TestBed.configureTestingModule({
      imports: [PromptDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => 'p1' }, queryParamMap: { get: () => null } },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(PromptDetail);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → load prompt + its datasets
    httpMock.expectOne('/api/prompts/p1').flush(prompt);
    httpMock.expectOne('/api/prompts/p1/version-status').flush(versionStatus);
    httpMock.expectOne('/api/prompts/p1/datasets').flush(datasets);
    httpMock.expectOne('/api/models').flush(models);
    // Datasets load → the Runs tab fetches each dataset's runs (prompt-wide runs list).
    httpMock.expectOne('/api/datasets/d1/eval-runs').flush([]);
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
    httpMock.expectOne('/api/datasets/d1/eval-runs').flush([]); // runs refresh after reload
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
    httpMock.expectOne('/api/datasets/d1/eval-runs').flush([]); // runs refresh after reload
  });

  it('runs a version against a dataset from the workspace [U13]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      showRun: { set: (v: boolean) => void };
      runVersionId: { set: (v: string) => void };
      onRunDatasetChange: (id: string) => void;
      triggerRun: (e: Event) => void;
    };
    cmp.showRun.set(true);
    cmp.runVersionId.set('v1');
    cmp.onRunDatasetChange('d1');
    // Selecting a dataset loads its recent runs inline.
    httpMock.expectOne('/api/datasets/d1/eval-runs').flush([]);

    cmp.triggerRun(new Event('submit'));
    const run = httpMock.expectOne('/api/datasets/d1/eval-runs');
    expect(run.request.method).toBe('POST');
    expect(run.request.body).toEqual({ promptId: 'p1', promptVersionId: 'v1' });
    run.flush({
      id: 'run-9',
      promptId: 'p1',
      promptVersionId: 'v1',
      datasetId: 'd1',
      createdAt: '2026-07-12T00:00:00Z',
      results: [],
    });
  });

  it('lists the prompt runs across its datasets in the Runs tab, loaded with the workspace', () => {
    // Like setup(), but the dataset's runs load with one run so the Runs table renders.
    TestBed.configureTestingModule({
      imports: [PromptDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => 'p1' }, queryParamMap: { get: () => null } },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(PromptDetail);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne('/api/prompts/p1').flush(prompt);
    httpMock.expectOne('/api/prompts/p1/version-status').flush(versionStatus);
    httpMock.expectOne('/api/prompts/p1/datasets').flush(datasets);
    httpMock.expectOne('/api/models').flush(models);
    // The Runs tab loads every dataset's runs on init — no need to open the run form.
    httpMock.expectOne('/api/datasets/d1/eval-runs').flush([
      {
        id: 'run-1',
        promptId: 'p1',
        promptVersionId: 'v1',
        createdAt: '2026-07-12T10:00:00Z',
        fixtureCount: 3,
        scoreCount: 6,
        scorerKinds: ['LlmJudge'],
        meanScore: 0.84,
        meanScorerKind: 'LlmJudge',
      },
    ]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const table = el.querySelector('[data-testid="prompt-runs"]');
    expect(table).toBeTruthy();
    expect(table!.textContent).toContain('Summaries'); // dataset name column
    expect(table!.textContent).toContain('v1'); // version resolved from promptVersionId
    expect(table!.textContent).toContain('0.84'); // meaningful mean
    expect(el.querySelector('[data-testid="no-prompt-runs"]')).toBeNull();
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
    httpMock.expectOne('/api/prompts/p1/version-status').flush(versionStatus); // status reload
  });

  it('marks a version "Current in source" and shows the badge + deployment marker [1.16]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as { setCurrent: (id: string) => void };
    cmp.setCurrent('v1');

    const post = httpMock.expectOne('/api/prompts/p1/versions/v1/set-current');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ commitSha: null });
    const currentStatus = {
      promptId: 'p1',
      currentVersionId: 'v1',
      backportTargetVersionId: null,
      versions: [
        {
          versionId: 'v1',
          versionNumber: 1,
          label: null,
          isCurrent: true,
          backportEligible: false,
          isBackportTarget: false,
          regressed: false,
        },
      ],
    };
    post.flush(currentStatus);
    // set-current reloads the prompt (getPrompt + version-status again).
    httpMock.expectOne('/api/prompts/p1').flush({ ...prompt, currentVersionId: 'v1' });
    httpMock.expectOne('/api/prompts/p1/version-status').flush(currentStatus);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="version-status"]')?.textContent).toContain('Current');
    expect(el.querySelector('[data-testid="deploy-current"]')?.textContent).toContain('v1');
  });

  it('defaults the add-version Target model to the latest version model, holding it [R5]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleAddVersion: () => void;
      targetModel: () => string;
      targetModelChanged: () => boolean;
    };
    cmp.toggleAddVersion();
    fixture.detectChanges();
    expect(cmp.targetModel()).toBe('legacy-model-x'); // v1's model — held by default
    expect(cmp.targetModelChanged()).toBe(false);
    expect(fixture.nativeElement.querySelector('[data-testid="model-change-warning"]')).toBeNull();
  });

  it('warns when the target-model SELECT is changed via a DOM change event [R5]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleAddVersion: () => void;
      targetModel: () => string;
      targetModelChanged: () => boolean;
    };
    cmp.toggleAddVersion();
    fixture.detectChanges();
    const select = fixture.nativeElement.querySelector(
      '[data-testid="target-model"]',
    ) as HTMLSelectElement;
    select.value = 'claude-sonnet-5'; // an option differing from v1's legacy-model-x
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(cmp.targetModel()).toBe('claude-sonnet-5');
    expect(cmp.targetModelChanged()).toBe(true);
    expect(
      fixture.nativeElement.querySelector('[data-testid="model-change-warning"]'),
    ).not.toBeNull();
  });

  it('warns when a new version changes the subject model [R5]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleAddVersion: () => void;
      targetModel: { set: (v: string) => void } & (() => string);
      targetModelChanged: () => boolean;
    };
    cmp.toggleAddVersion();
    cmp.targetModel.set('claude-sonnet-5'); // differs from v1's legacy-model-x
    fixture.detectChanges();
    expect(cmp.targetModelChanged()).toBe(true);
    const warn = fixture.nativeElement.querySelector('[data-testid="model-change-warning"]');
    expect(warn).not.toBeNull();
    expect(warn.textContent).toContain('legacy-model-x'); // names the held (prior) model
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

  // 2.11 — Cancel on the reveal/expand surfaces (add-version, create-dataset, edit-label, run).
  it('cancels the add-version form, discarding seeded/typed content and collapsing it [2.11]', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    const cmp = fixture.componentInstance as unknown as {
      toggleAddVersion: () => void;
      cancelAddVersion: () => void;
      content: () => string;
      label: { set: (v: string) => void } & (() => string);
      showAddVersion: () => boolean;
    };
    cmp.toggleAddVersion(); // opens + seeds content from the latest version (U11)
    fixture.detectChanges();
    expect(cmp.content()).toBe('Summarize: {input}');
    cmp.label.set('wip');

    // Cancel is paired with the submit.
    expect(el.querySelector('[data-testid="cancel-add-version"]')).not.toBeNull();

    cmp.cancelAddVersion();
    fixture.detectChanges();
    expect(cmp.showAddVersion()).toBe(false);
    expect(cmp.content()).toBe('');
    expect(cmp.label()).toBe('');
    expect(el.querySelector('[data-testid="add-version"]')).toBeNull(); // collapsed
  });

  it('cancels an open version-label editor, collapsing the row [2.11]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleVersion: (id: string) => void;
      cancelEditLabel: () => void;
      expandedVersionId: () => string | null;
    };
    cmp.toggleVersion('v1');
    fixture.detectChanges();
    expect(cmp.expandedVersionId()).toBe('v1');
    expect(fixture.nativeElement.querySelector('[data-testid="cancel-edit-label"]')).not.toBeNull();

    cmp.cancelEditLabel();
    fixture.detectChanges();
    expect(cmp.expandedVersionId()).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="version-detail"]')).toBeNull();
  });

  it('cancels the create-dataset form, clearing input without a POST [2.11]', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      showCreateDataset: { set: (v: boolean) => void } & (() => boolean);
      datasetName: { set: (v: string) => void } & (() => string);
      cancelCreateDataset: () => void;
    };
    cmp.showCreateDataset.set(true);
    fixture.detectChanges();
    cmp.datasetName.set('Scratch');
    cmp.cancelCreateDataset();
    fixture.detectChanges();

    expect(cmp.showCreateDataset()).toBe(false);
    expect(cmp.datasetName()).toBe('');
    httpMock.expectNone('/api/prompts/p1/datasets'); // beyond the initial load, nothing persisted
  });

  it('edits Content through the markdown editor on add-version, but not in-place [2.10]', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    const cmp = fixture.componentInstance as unknown as {
      toggleVersion: (id: string) => void;
      toggleAddVersion: () => void;
    };

    // An existing version's content is immutable — shown in a <pre>, never an editor.
    cmp.toggleVersion('v1');
    fixture.detectChanges();
    const detail = el.querySelector('[data-testid="version-detail"]')!;
    expect(detail.querySelector('.version-content')).not.toBeNull(); // read-only <pre>
    expect(detail.querySelector('app-markdown-editor')).toBeNull(); // not editable in place

    // The markdown editor only appears on the add-version (new-version) path.
    cmp.toggleVersion('v1'); // collapse
    cmp.toggleAddVersion();
    fixture.detectChanges();
    const editor = el.querySelector('app-markdown-editor');
    expect(editor).not.toBeNull();
    // Its source textarea keeps the #content id so import/e2e fills still target it.
    expect(editor!.querySelector('textarea#content')).not.toBeNull();
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

  // --- 1.20 Backport assistance -------------------------------------------------------------------
  const promptTwo = {
    ...prompt,
    currentVersionId: 'v1',
    currentVersionSha: 'abc1234',
    versions: [
      { ...prompt.versions[0], content: 'Summarize: {input}' },
      {
        id: 'v2',
        versionNumber: 2,
        content: 'Summarize concisely: {input}',
        targetModel: 'claude-opus-4-8',
        label: null,
        sourceApp: null,
        createdAt: '2026-07-13T00:00:00Z',
      },
    ],
  };
  const statusTargeted = {
    promptId: 'p1',
    currentVersionId: 'v1',
    backportTargetVersionId: 'v2',
    versions: [
      {
        versionId: 'v1',
        versionNumber: 1,
        label: null,
        isCurrent: true,
        backportEligible: false,
        isBackportTarget: false,
        regressed: false,
      },
      {
        versionId: 'v2',
        versionNumber: 2,
        label: null,
        isCurrent: false,
        backportEligible: true,
        isBackportTarget: true,
        regressed: false,
      },
    ],
  };
  const artifact = {
    promptId: 'p1',
    promptName: 'Summarizer',
    currentVersionNumber: 1,
    currentVersionSha: 'abc1234',
    targetVersionNumber: 2,
    targetModel: 'claude-opus-4-8',
    content: 'Summarize concisely: {input}',
    diff: [
      { kind: 'removed', text: 'Summarize: {input}' },
      { kind: 'added', text: 'Summarize concisely: {input}' },
    ],
    scoreDeltas: [
      {
        datasetName: 'Summaries',
        scorerLabel: 'Regex',
        currentMean: 0.5,
        targetMean: 0.9,
        delta: 0.4,
      },
    ],
    markdown: '# Backport: Summarizer\n\nSummarize concisely: {input}\n',
    fileName: 'backport-summarizer-v1-to-v2.md',
  };

  function setupTargeted() {
    TestBed.configureTestingModule({
      imports: [PromptDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => 'p1' }, queryParamMap: { get: () => null } },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(PromptDetail);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne('/api/prompts/p1').flush(promptTwo);
    httpMock.expectOne('/api/prompts/p1/version-status').flush(statusTargeted);
    httpMock.expectOne('/api/prompts/p1/datasets').flush(datasets);
    httpMock.expectOne('/api/models').flush(models);
    httpMock.expectOne('/api/datasets/d1/eval-runs').flush([]);
    fixture.detectChanges();
    return fixture;
  }

  it('offers "Prepare backport" only when a backport target exists [1.20]', () => {
    const withTarget = setupTargeted();
    expect(
      (withTarget.nativeElement as HTMLElement).querySelector('[data-testid="prepare-backport"]'),
    ).not.toBeNull();

    TestBed.resetTestingModule();
    const noTarget = setup(); // base fixtures — no target
    expect(
      (noTarget.nativeElement as HTMLElement).querySelector('[data-testid="prepare-backport"]'),
    ).toBeNull();
  });

  it('fetches the artifact and shows the markdown preview [1.20]', () => {
    const fixture = setupTargeted();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="prepare-backport"]')!
      .click();
    fixture.detectChanges();

    const req = httpMock.expectOne('/api/prompts/p1/backport-artifact');
    expect(req.request.method).toBe('GET');
    req.flush(artifact);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="backport-drawer"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="backport-markdown"]')?.textContent).toContain(
      '# Backport: Summarizer',
    );
  });

  it('copies the exact target content to the clipboard [1.20]', () => {
    const writeText = jasmine.createSpy('writeText').and.returnValue(Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    const fixture = setupTargeted();
    const cmp = fixture.componentInstance as unknown as {
      copyExactPrompt: (a: typeof artifact) => void;
    };
    cmp.copyExactPrompt(artifact);

    expect(writeText).toHaveBeenCalledWith('Summarize concisely: {input}');
  });

  it('downloads the markdown as a .md blob [1.20]', () => {
    const createUrl = spyOn(URL, 'createObjectURL').and.returnValue('blob:x');
    spyOn(URL, 'revokeObjectURL');
    const click = spyOn(HTMLAnchorElement.prototype, 'click');

    const fixture = setupTargeted();
    const cmp = fixture.componentInstance as unknown as {
      downloadMarkdown: (a: typeof artifact) => void;
    };
    cmp.downloadMarkdown(artifact);

    expect(createUrl).toHaveBeenCalledWith(jasmine.any(Blob));
    expect(click).toHaveBeenCalled();
  });

  it('warns when versions are excluded from the backport comparison for a different model [2.9a/R9]', () => {
    // Default targeted status has no cross-model exclusions → no warning.
    const noWarn = setupTargeted();
    expect(
      (noWarn.nativeElement as HTMLElement).querySelector('[data-testid="cross-model-warning"]'),
    ).toBeNull();

    TestBed.resetTestingModule();

    // A status that held 2 cross-model versions out of the comparison → the warning renders.
    TestBed.configureTestingModule({
      imports: [PromptDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => 'p1' }, queryParamMap: { get: () => null } },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(PromptDetail);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne('/api/prompts/p1').flush(promptTwo);
    httpMock
      .expectOne('/api/prompts/p1/version-status')
      .flush({ ...statusTargeted, crossModelVersionsExcluded: 2 });
    httpMock.expectOne('/api/prompts/p1/datasets').flush(datasets);
    httpMock.expectOne('/api/models').flush(models);
    httpMock.expectOne('/api/datasets/d1/eval-runs').flush([]);
    fixture.detectChanges();

    const warn = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="cross-model-warning"]',
    );
    expect(warn).not.toBeNull();
    expect(warn?.textContent?.replace(/\s+/g, ' ')).toContain('different subject model');
  });
});
