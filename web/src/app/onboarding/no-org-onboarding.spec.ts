import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { NoOrgOnboarding } from './no-org-onboarding';

describe('NoOrgOnboarding (zero-org onboarding, spec 2.21)', () => {
  let httpMock: HttpTestingController;

  const directory = [
    { id: 'o1', name: 'Acme', isMember: false, hasPendingRequest: false },
    { id: 'o2', name: 'Globex', isMember: false, hasPendingRequest: true },
  ];

  function setup() {
    TestBed.configureTestingModule({
      imports: [NoOrgOnboarding],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(NoOrgOnboarding);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // constructor → load directory
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('offers both paths: a create form and a directory to request access', () => {
    const fixture = setup();
    httpMock.expectOne('/api/organizations/directory').flush(directory);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="onboarding-create-org"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="onboarding-directory"]')).not.toBeNull();
    // Acme is joinable (a Request button); Globex already shows "Requested".
    expect(el.querySelector('[data-testid="request-access"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="dir-requested"]')).not.toBeNull();
  });

  it('requests access to an org and reflects the pending state', () => {
    const fixture = setup();
    httpMock.expectOne('/api/organizations/directory').flush(directory);
    fixture.detectChanges();

    const cmp = fixture.componentInstance as unknown as { requestAccess: (id: string) => void };
    cmp.requestAccess('o1');

    const req = httpMock.expectOne('/api/organizations/o1/access-requests');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ role: 'Member' });
    req.flush({ id: 'r1' });
    fixture.detectChanges();

    // The row for o1 now shows "Requested", not a Request button.
    const row = fixture.nativeElement.querySelector('[data-org-id="o1"]') as HTMLElement;
    expect(row.querySelector('[data-testid="dir-requested"]')).not.toBeNull();
    expect(row.querySelector('[data-testid="request-access"]')).toBeNull();
  });

  it('creates an organization (POSTs the name)', () => {
    const fixture = setup();
    httpMock.expectOne('/api/organizations/directory').flush([]);
    fixture.detectChanges();

    const cmp = fixture.componentInstance as unknown as {
      orgName: { set: (v: string) => void };
      createOrg: (e: Event) => void;
    };
    cmp.orgName.set('New Team');
    cmp.createOrg(new Event('submit'));

    const req = httpMock.expectOne('/api/organizations');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'New Team' });
    req.flush({ id: 'o9', name: 'New Team' });
    // OrgContextStore.add navigates; nothing else to assert here.
  });
});
