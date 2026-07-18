import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { OrgContextStore } from '../shared/org-context.store';
import { ConfirmService } from '../shared';
import { PromptList } from './prompt-list';

describe('PromptList (org + folder navigation)', () => {
  let httpMock: HttpTestingController;

  const orgs = [{ id: 'o1', name: 'Acme' }];
  const folders = [
    { id: 'f1', parentId: null, name: 'Marketing' },
    { id: 'f2', parentId: 'f1', name: 'Blog' },
  ];
  const prompts = [
    {
      id: 'p1',
      folderId: null,
      name: 'Root prompt',
      description: null,
      versionCount: 1,
      latestTargetModel: 'opus',
    },
    {
      id: 'p2',
      folderId: 'f1',
      name: 'Marketing prompt',
      description: null,
      versionCount: 2,
      latestTargetModel: null,
    },
    {
      id: 'p3',
      folderId: 'f2',
      name: 'Blog prompt',
      description: null,
      versionCount: 0,
      latestTargetModel: null,
    },
  ];

  function flushOrgData() {
    httpMock.expectOne('/api/organizations/o1/folders').flush(folders);
    httpMock.expectOne('/api/organizations/o1/prompts').flush(prompts);
  }

  function setup() {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [PromptList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: OrganizationsApiService, useValue: { listOrganizations: () => of(orgs) } },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    TestBed.inject(OrgContextStore).load(); // resolves the global org context → o1
    const fixture = TestBed.createComponent(PromptList);
    fixture.detectChanges(); // org effect → loads folders + prompts for o1
    flushOrgData();
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('navigates to the new prompt’s workspace after create [U1]', () => {
    const fixture = setup();
    const navSpy = spyOn(TestBed.inject(Router), 'navigate');
    const cmp = fixture.componentInstance as unknown as {
      name: { set: (v: string) => void };
      createPrompt: (e: Event) => void;
    };
    cmp.name.set('Brand new prompt');
    cmp.createPrompt(new Event('submit'));

    const create = httpMock.expectOne('/api/organizations/o1/prompts');
    expect(create.request.method).toBe('POST');
    create.flush({
      id: 'p9',
      folderId: null,
      name: 'Brand new prompt',
      description: null,
      versions: [],
    });

    expect(navSpy).toHaveBeenCalledWith(['/prompts', 'p9']);
  });

  it('shows the current folder’s subfolders and prompts (org root by default)', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    // root subfolder Marketing (parentId null) is shown; Blog (under Marketing) is not
    expect(el.querySelector('[data-testid="subfolder-f1"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="subfolder-f2"]')).toBeFalsy();
    // only the root-level prompt shows
    const rows = el.querySelectorAll('[data-testid="prompts"] tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Root prompt');
  });

  it('descends into a subfolder and updates the breadcrumb and contents', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    (el.querySelector('[data-testid="subfolder-f1"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el.querySelector('[data-testid="breadcrumb"]')?.textContent).toContain('Acme');
    expect(el.querySelector('[data-testid="breadcrumb"]')?.textContent).toContain('Marketing');
    // now Blog (child of Marketing) is a visible subfolder, and the Marketing prompt shows
    expect(el.querySelector('[data-testid="subfolder-f2"]')).toBeTruthy();
    const rows = el.querySelectorAll('[data-testid="prompts"] tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Marketing prompt');
  });

  it('rescopes to the new org on switch and ignores a stale in-flight response for the old org', async () => {
    localStorage.clear();
    const twoOrgs = [
      { id: 'o1', name: 'Acme' },
      { id: 'o2', name: 'Globex' },
    ];
    TestBed.configureTestingModule({
      imports: [PromptList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: OrganizationsApiService, useValue: { listOrganizations: () => of(twoOrgs) } },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    const store = TestBed.inject(OrgContextStore);
    store.load(); // resolves to o1
    const fixture = TestBed.createComponent(PromptList);
    fixture.detectChanges();

    // o1's requests are still in flight — capture them without responding.
    const o1folders = httpMock.expectOne('/api/organizations/o1/folders');
    const o1prompts = httpMock.expectOne('/api/organizations/o1/prompts');

    // Switch to o2 before o1 responds.
    store.select('o2');
    await fixture.whenStable();
    fixture.detectChanges();
    const o2folders = httpMock.expectOne('/api/organizations/o2/folders');
    const o2prompts = httpMock.expectOne('/api/organizations/o2/prompts');

    // o2 responds first…
    o2folders.flush([]);
    o2prompts.flush([
      { id: 'p2', folderId: null, name: 'O2 prompt', description: null, versionCount: 0 },
    ]);
    // …then the slower, now-stale o1 responses arrive.
    o1folders.flush([]);
    o1prompts.flush([
      { id: 'p1', folderId: null, name: 'O1 STALE prompt', description: null, versionCount: 0 },
    ]);
    fixture.detectChanges();

    // The current org's data must win — the stale o1 response is dropped.
    const rows = fixture.nativeElement.querySelectorAll('[data-testid="prompts"] tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('O2 prompt');
  });

  it('bulk-imports prompts by looping create/add-version POSTs, one report row each', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      showImport: { set: (v: boolean) => void };
      importResults: () => { name: string; ok: boolean }[];
      runBulkImport: (orgId: string, text: string) => void;
    };
    cmp.showImport.set(true);

    cmp.runBulkImport(
      'o1',
      JSON.stringify([
        { name: 'Alpha', versions: [{ content: 'A: {x}', targetModel: 'claude-sonnet-5' }] },
        { name: 'Beta' }, // no versions — just the prompt
      ]),
    );

    // Alpha: create, then its one version.
    const createA = httpMock.expectOne('/api/organizations/o1/prompts');
    expect(createA.request.body).toEqual({ name: 'Alpha', description: null });
    createA.flush({ id: 'pa', folderId: null, name: 'Alpha', description: null, versions: [] });
    const versionA = httpMock.expectOne('/api/prompts/pa/versions');
    expect(versionA.request.body).toEqual({
      content: 'A: {x}',
      targetModel: 'claude-sonnet-5',
      label: null,
      sourceApp: null,
    });
    versionA.flush({});

    // Beta: create only (no versions POST).
    httpMock
      .expectOne('/api/organizations/o1/prompts')
      .flush({ id: 'pb', folderId: null, name: 'Beta', description: null, versions: [] });

    flushOrgData(); // reload after the import completes

    const results = cmp.importResults();
    expect(results.map((r) => r.name)).toEqual(['Alpha', 'Beta']);
    expect(results.every((r) => r.ok)).toBe(true);

    fixture.detectChanges();
    const rows = fixture.nativeElement.querySelectorAll('[data-testid="import-results"] tbody tr');
    expect(rows.length).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('[data-testid="import-ok"]').length).toBe(2);
  });

  it('reports a per-row error and keeps importing when one prompt fails', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      importResults: () => { name: string; ok: boolean }[];
      runBulkImport: (orgId: string, text: string) => void;
    };

    cmp.runBulkImport('o1', JSON.stringify([{ name: 'Bad' }, { name: 'Good' }]));

    // First prompt's create fails…
    httpMock
      .expectOne('/api/organizations/o1/prompts')
      .flush('boom', { status: 500, statusText: 'Server Error' });
    // …the second still runs.
    httpMock
      .expectOne('/api/organizations/o1/prompts')
      .flush({ id: 'pg', folderId: null, name: 'Good', description: null, versions: [] });
    flushOrgData();

    const results = cmp.importResults();
    expect(results.length).toBe(2);
    expect(results[0]).toEqual(jasmine.objectContaining({ name: 'Bad', ok: false }));
    expect(results[1]).toEqual(jasmine.objectContaining({ name: 'Good', ok: true }));
  });

  it('surfaces a parse error and issues no requests for a malformed file', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      error: () => string | null;
      runBulkImport: (orgId: string, text: string) => void;
    };
    cmp.runBulkImport('o1', '{not json');
    expect(cmp.error()).toContain('valid JSON');
    // afterEach httpMock.verify() asserts no create/version requests went out.
  });

  it('moves a prompt to a folder via its row select', () => {
    const fixture = setup();
    const select: HTMLSelectElement =
      fixture.nativeElement.querySelector('[data-testid="move-p1"]');
    select.value = 'f1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const move = httpMock.expectOne('/api/prompts/p1/move');
    expect(move.request.body).toEqual({ folderId: 'f1' });
    move.flush({ id: 'p1', folderId: 'f1', name: 'Root prompt', description: null, versions: [] });
    flushOrgData(); // reload after move
  });

  it('confirms (stating the cascade) before deleting a prompt, then reloads the list', async () => {
    const fixture = setup();
    const confirm = TestBed.inject(ConfirmService);
    const askSpy = spyOn(confirm, 'ask').and.returnValue(Promise.resolve(true));

    const el: HTMLElement = fixture.nativeElement;
    (el.querySelector('[data-testid="delete-prompt-p1"]') as HTMLButtonElement).click();

    // The confirmation states what cascades.
    expect(askSpy).toHaveBeenCalledTimes(1);
    const message = askSpy.calls.mostRecent().args[0].message;
    expect(message).toContain('Root prompt');
    expect(message).toContain('version');

    await fixture.whenStable();
    const del = httpMock.expectOne('/api/prompts/p1');
    expect(del.request.method).toBe('DELETE');
    del.flush(null);
    flushOrgData(); // list reloads after delete
  });

  it('does not delete a prompt when the confirmation is cancelled', async () => {
    const fixture = setup();
    const confirm = TestBed.inject(ConfirmService);
    spyOn(confirm, 'ask').and.returnValue(Promise.resolve(false));

    const el: HTMLElement = fixture.nativeElement;
    (el.querySelector('[data-testid="delete-prompt-p1"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    // No DELETE issued — httpMock.verify() in afterEach asserts nothing outstanding.
    httpMock.expectNone('/api/prompts/p1');
  });
});
