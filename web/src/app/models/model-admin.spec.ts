import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { ModelAdmin } from './model-admin';

describe('ModelAdmin (catalog management)', () => {
  let httpMock: HttpTestingController;

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
      modelId: 'gpt-legacy',
      displayName: 'GPT legacy',
      provider: 'OpenAi',
      roles: ['subject'],
      inputPricePerMTokUsd: null,
      outputPricePerMTokUsd: null,
      isActive: false,
      available: true,
    },
  ];

  function setup() {
    TestBed.configureTestingModule({
      imports: [ModelAdmin],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(ModelAdmin);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → load all (incl inactive)
    httpMock.expectOne('/api/models?includeInactive=true').flush(models);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('lists the catalog including inactive entries', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    const table = el.querySelector('[data-testid="models-admin-table"]');
    expect(table?.textContent).toContain('claude-opus-4-8');
    expect(table?.textContent).toContain('gpt-legacy'); // inactive still shown
  });

  it('creates a model, posting the parsed body, then reloads', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      startCreate: () => void;
      fModelId: { set: (v: string) => void };
      fDisplayName: { set: (v: string) => void };
      fJudge: { set: (v: boolean) => void };
      save: (e: Event) => void;
    };
    cmp.startCreate();
    cmp.fModelId.set('gpt-4o-mini');
    cmp.fDisplayName.set('GPT-4o mini');
    cmp.fJudge.set(true); // subject is on by default
    cmp.save(new Event('submit'));

    const create = httpMock.expectOne('/api/models');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({
      displayName: 'GPT-4o mini',
      provider: 'Anthropic',
      roles: ['subject', 'judge'],
      inputPricePerMTokUsd: null,
      outputPricePerMTokUsd: null,
      modelId: 'gpt-4o-mini',
    });
    create.flush({ ...models[0], id: 'm3', modelId: 'gpt-4o-mini' });
    httpMock.expectOne('/api/models?includeInactive=true').flush(models); // reload
  });

  it('deactivates an active model via the activate/deactivate endpoint, then reloads', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as { toggleActive: (m: unknown) => void };
    cmp.toggleActive(models[0]); // active → deactivate

    const req = httpMock.expectOne('/api/models/m1/deactivate');
    expect(req.request.method).toBe('POST');
    req.flush({ ...models[0], isActive: false });
    httpMock.expectOne('/api/models?includeInactive=true').flush(models); // reload
  });
});
