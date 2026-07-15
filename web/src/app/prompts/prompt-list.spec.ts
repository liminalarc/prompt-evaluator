import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
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
