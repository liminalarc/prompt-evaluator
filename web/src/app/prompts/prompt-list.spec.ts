import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
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
    TestBed.configureTestingModule({
      imports: [PromptList],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(PromptList);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → list orgs
    httpMock.expectOne('/api/organizations').flush(orgs);
    flushOrgData(); // selectOrg(first) → folders + prompts
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('lists the organization in the switcher', () => {
    const fixture = setup();
    const options = fixture.nativeElement.querySelectorAll('[data-testid="org-select"] option');
    expect(Array.from(options).some((o: any) => o.textContent.includes('Acme'))).toBeTrue();
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
});
