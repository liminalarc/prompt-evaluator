import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { OrgDetail } from './org-detail';

describe('OrgDetail (owner-facing member management, spec 4.5)', () => {
  let httpMock: HttpTestingController;

  const orgsAsOwner = [{ id: 'o1', name: 'Acme', role: 'Owner' }];
  const orgsAsMember = [{ id: 'o1', name: 'Acme', role: 'Member' }];
  const members = [
    { userId: 'u1', email: 'alice@test.local', displayName: 'Alice', role: 'Owner' },
    { userId: 'u2', email: 'bob@test.local', displayName: 'Bob', role: 'Member' },
  ];

  function setup() {
    TestBed.configureTestingModule({
      imports: [OrgDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', 'o1']]) } } },
      ],
    });
    const fixture = TestBed.createComponent(OrgDetail);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → load org list (for name + my role)
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('lists the org’s members when the caller is an owner', () => {
    const fixture = setup();
    httpMock.expectOne('/api/organizations').flush(orgsAsOwner);
    httpMock.expectOne('/api/organizations/o1/members').flush(members);
    fixture.detectChanges();

    const table = fixture.nativeElement.querySelector('[data-testid="members-table"]');
    expect(table.textContent).toContain('alice@test.local');
    expect(table.textContent).toContain('bob@test.local');
    expect(fixture.nativeElement.textContent).toContain('Acme');
  });

  it('adds a member by email, then reloads members', () => {
    const fixture = setup();
    httpMock.expectOne('/api/organizations').flush(orgsAsOwner);
    httpMock.expectOne('/api/organizations/o1/members').flush(members);

    const cmp = fixture.componentInstance as unknown as {
      setAddEmail: (v: string) => void;
      setAddRole: (v: string) => void;
      addMember: () => void;
    };
    cmp.setAddEmail('carol@test.local');
    cmp.setAddRole('Member');
    cmp.addMember();

    const req = httpMock.expectOne('/api/organizations/o1/members');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'carol@test.local', role: 'Member' });
    req.flush(null);
    httpMock.expectOne('/api/organizations/o1/members').flush(members); // reload
  });

  it('sets a member’s role, then reloads', () => {
    const fixture = setup();
    httpMock.expectOne('/api/organizations').flush(orgsAsOwner);
    httpMock.expectOne('/api/organizations/o1/members').flush(members);

    const cmp = fixture.componentInstance as unknown as {
      setRole: (userId: string, role: string) => void;
    };
    cmp.setRole('u2', 'Owner');

    const req = httpMock.expectOne('/api/organizations/o1/members/u2');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ role: 'Owner' });
    req.flush(null);
    httpMock.expectOne('/api/organizations/o1/members').flush(members); // reload
  });

  it('removes a member, then reloads', () => {
    const fixture = setup();
    httpMock.expectOne('/api/organizations').flush(orgsAsOwner);
    httpMock.expectOne('/api/organizations/o1/members').flush(members);

    const cmp = fixture.componentInstance as unknown as { removeMember: (userId: string) => void };
    cmp.removeMember('u2');

    const req = httpMock.expectOne('/api/organizations/o1/members/u2');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    httpMock.expectOne('/api/organizations/o1/members').flush(members); // reload
  });

  it('shows a permission notice and fetches no members for a plain member', () => {
    const fixture = setup();
    httpMock.expectOne('/api/organizations').flush(orgsAsMember);
    fixture.detectChanges();

    // No member list is fetched (server would 403); a notice is shown instead.
    httpMock.verify();
    expect(fixture.nativeElement.querySelector('[data-testid="members-table"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="no-permission"]')).not.toBeNull();
  });

  it('surfaces an add error (e.g. unknown email)', () => {
    const fixture = setup();
    httpMock.expectOne('/api/organizations').flush(orgsAsOwner);
    httpMock.expectOne('/api/organizations/o1/members').flush(members);

    const cmp = fixture.componentInstance as unknown as {
      setAddEmail: (v: string) => void;
      addMember: () => void;
    };
    cmp.setAddEmail('nobody@test.local');
    cmp.addMember();
    httpMock
      .expectOne('/api/organizations/o1/members')
      .flush({ error: 'No user with that email.' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="error"]')).not.toBeNull();
  });

  it('does not depend on the global admin flag to gate — owner role alone suffices', () => {
    const fixture = setup();
    const auth = TestBed.inject(AuthService);
    expect(auth.currentUser()).toBeNull(); // not admin
    httpMock.expectOne('/api/organizations').flush(orgsAsOwner);
    httpMock.expectOne('/api/organizations/o1/members').flush(members); // still allowed as owner
  });
});
