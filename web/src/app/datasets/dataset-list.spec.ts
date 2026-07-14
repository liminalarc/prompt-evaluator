import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { DatasetList } from './dataset-list';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { OrgContextStore } from '../shared/org-context.store';

describe('DatasetList (org-scoped browse)', () => {
  let http: HttpTestingController;

  function setup() {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [DatasetList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: OrganizationsApiService,
          useValue: { listOrganizations: () => of([{ id: 'o1', name: 'Acme' }]) },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(OrgContextStore).load(); // → current org o1
    const fixture = TestBed.createComponent(DatasetList);
    fixture.detectChanges(); // org effect → load
    return fixture;
  }

  afterEach(() => http.verify());

  it('shows only datasets belonging to the current org’s prompts', () => {
    const fixture = setup();
    http.expectOne('/api/organizations/o1/prompts').flush([
      {
        id: 'p1',
        folderId: null,
        name: 'A',
        description: null,
        versionCount: 1,
        latestTargetModel: null,
      },
    ]);
    http.expectOne('/api/datasets').flush([
      {
        id: 'd1',
        promptId: 'p1',
        name: 'In org',
        description: null,
        fixtureCount: 1,
        capturedCount: 1,
        syntheticCount: 0,
      },
      {
        id: 'd2',
        promptId: 'pX',
        name: 'Other org',
        description: null,
        fixtureCount: 1,
        capturedCount: 0,
        syntheticCount: 1,
      },
    ]);
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('[data-testid="datasets"] tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('In org');
    expect(fixture.nativeElement.textContent).not.toContain('Other org');
  });

  it('shows the empty state when the org has no datasets', () => {
    const fixture = setup();
    http.expectOne('/api/organizations/o1/prompts').flush([
      {
        id: 'p1',
        folderId: null,
        name: 'A',
        description: null,
        versionCount: 0,
        latestTargetModel: null,
      },
    ]);
    http.expectOne('/api/datasets').flush([]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="empty"]')).toBeTruthy();
  });
});
