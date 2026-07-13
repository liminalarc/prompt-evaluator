import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { PromptList } from './prompt-list';

describe('PromptList (folder browse)', () => {
  let httpMock: HttpTestingController;

  const folders = [
    { id: 'f1', parentId: null, name: 'Stormboard' },
    { id: 'f2', parentId: 'f1', name: 'Summarization' },
  ];
  const prompts = [
    { id: 'p1', folderId: null, name: 'Unfiled one', description: null, versionCount: 1, latestTargetModel: 'claude-sonnet-5' },
    { id: 'p2', folderId: 'f1', name: 'In Stormboard', description: null, versionCount: 2, latestTargetModel: null },
    { id: 'p3', folderId: 'f2', name: 'In Summarization', description: null, versionCount: 0, latestTargetModel: null },
  ];

  function setup() {
    TestBed.configureTestingModule({
      imports: [PromptList],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(PromptList);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → forkJoin
    httpMock.expectOne('/api/prompts').flush(prompts);
    httpMock.expectOne('/api/folders').flush(folders);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('renders the folder tree with a Root node plus every folder', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('[data-testid="folder-root"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="folder-f1"]')?.textContent).toContain('Stormboard');
    expect(el.querySelector('[data-testid="folder-f2"]')?.textContent).toContain('Summarization');
  });

  it('defaults to Root and shows only unfiled prompts', () => {
    const fixture = setup();
    const rows = fixture.nativeElement.querySelectorAll('[data-testid="prompts"] tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Unfiled one');
  });

  it('filters to a folder and updates the breadcrumb when a folder is selected', () => {
    const fixture = setup();
    const el: HTMLElement = fixture.nativeElement;
    (el.querySelector('[data-testid="folder-f2"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el.querySelector('[data-testid="breadcrumb"]')?.textContent).toContain(
      'Root / Stormboard / Summarization',
    );
    const rows = el.querySelectorAll('[data-testid="prompts"] tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('In Summarization');
  });

  it('moves a prompt to a folder via its row select', () => {
    const fixture = setup();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('[data-testid="move-p1"]');
    select.value = 'f1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const move = httpMock.expectOne('/api/prompts/p1/move');
    expect(move.request.body).toEqual({ folderId: 'f1' });
    move.flush({ id: 'p1', folderId: 'f1', name: 'Unfiled one', description: null, versions: [] });
    // reload after move
    httpMock.expectOne('/api/prompts').flush(prompts);
    httpMock.expectOne('/api/folders').flush(folders);
  });
});
