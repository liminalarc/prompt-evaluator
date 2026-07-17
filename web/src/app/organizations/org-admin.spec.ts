import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { OrgAdmin } from './org-admin';

describe('OrgAdmin (admin organization management, spec 4.4)', () => {
  let httpMock: HttpTestingController;

  const orgs = [
    { id: 'o1', name: 'Acme', memberCount: 2 },
    { id: 'o2', name: 'Beta', memberCount: 0 },
  ];
  const users = [
    { id: 'u1', email: 'admin@test.local', displayName: 'Admin', isAdmin: true, memberships: [] },
    {
      id: 'u2',
      email: 'member@test.local',
      displayName: 'Member',
      isAdmin: false,
      memberships: [],
    },
  ];

  function setup() {
    TestBed.configureTestingModule({
      imports: [OrgAdmin],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(OrgAdmin);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → load orgs + users (for the add-member picker)
    httpMock.expectOne('/api/admin/organizations').flush(orgs);
    httpMock.expectOne('/api/admin/users').flush(users);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('lists organizations with their member counts', () => {
    const fixture = setup();
    const table = fixture.nativeElement.querySelector('[data-testid="orgs-admin-table"]');
    expect(table.textContent).toContain('Acme');
    expect(table.textContent).toContain('Beta');
    expect(table.textContent).toContain('2'); // Acme member count
  });

  it('creates an organization, then reloads', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      newName: (v: string) => void;
      createOrg: () => void;
    };
    cmp.newName('Gamma');
    cmp.createOrg();

    const req = httpMock.expectOne('/api/admin/organizations');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Gamma' });
    req.flush({ id: 'o3', name: 'Gamma', memberCount: 0 });
    httpMock.expectOne('/api/admin/organizations').flush(orgs); // reload
  });

  it('renames an organization, then reloads', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      startRename: (o: unknown) => void;
      setRename: (id: string, v: string) => void;
      saveRename: (o: unknown) => void;
    };
    cmp.startRename(orgs[0]);
    cmp.setRename('o1', 'Acme Corp');
    cmp.saveRename(orgs[0]);

    const req = httpMock.expectOne('/api/admin/organizations/o1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ name: 'Acme Corp' });
    req.flush({ id: 'o1', name: 'Acme Corp', memberCount: 2 });
    httpMock.expectOne('/api/admin/organizations').flush(orgs); // reload
  });

  it('blocks delete until the typed name matches, then deletes and reloads', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      startDelete: (o: unknown) => void;
      setDeleteConfirm: (id: string, v: string) => void;
      canDelete: (o: { id: string; name: string }) => boolean;
      deleteOrg: (o: unknown) => void;
    };
    cmp.startDelete(orgs[0]);
    // Wrong name → guarded, no request.
    cmp.setDeleteConfirm('o1', 'wrong');
    expect(cmp.canDelete(orgs[0])).toBe(false);
    cmp.deleteOrg(orgs[0]);
    httpMock.verify(); // asserts nothing was sent

    // Exact name → enabled and deletes.
    cmp.setDeleteConfirm('o1', 'Acme');
    expect(cmp.canDelete(orgs[0])).toBe(true);
    cmp.deleteOrg(orgs[0]);
    const req = httpMock.expectOne('/api/admin/organizations/o1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    httpMock.expectOne('/api/admin/organizations').flush(orgs); // reload
  });

  it('loads an org’s members on expand', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as { toggleMembers: (o: unknown) => void };
    cmp.toggleMembers(orgs[0]);

    const req = httpMock.expectOne('/api/admin/organizations/o1/members');
    expect(req.request.method).toBe('GET');
    req.flush([
      { userId: 'u2', email: 'member@test.local', displayName: 'Member', role: 'Member' },
    ]);
  });

  it('adds a member to an org, then reloads that org’s members', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleMembers: (o: unknown) => void;
      setAddUser: (orgId: string, userId: string) => void;
      setAddRole: (orgId: string, role: string) => void;
      addMember: (o: unknown) => void;
    };
    cmp.toggleMembers(orgs[0]);
    httpMock.expectOne('/api/admin/organizations/o1/members').flush([]);

    cmp.setAddUser('o1', 'u2');
    cmp.setAddRole('o1', 'Member');
    cmp.addMember(orgs[0]);

    const req = httpMock.expectOne('/api/admin/organizations/o1/members');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'u2', role: 'Member' });
    req.flush(null);
    httpMock.expectOne('/api/admin/organizations/o1/members').flush([]); // reload members
    httpMock.expectOne('/api/admin/organizations').flush(orgs); // reload counts
  });

  it('removes a member from an org, then reloads', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleMembers: (o: unknown) => void;
      removeMember: (o: unknown, userId: string) => void;
    };
    cmp.toggleMembers(orgs[0]);
    httpMock
      .expectOne('/api/admin/organizations/o1/members')
      .flush([{ userId: 'u2', email: 'member@test.local', displayName: 'Member', role: 'Member' }]);

    cmp.removeMember(orgs[0], 'u2');
    const req = httpMock.expectOne('/api/admin/organizations/o1/members/u2');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    httpMock.expectOne('/api/admin/organizations/o1/members').flush([]); // reload members
    httpMock.expectOne('/api/admin/organizations').flush(orgs); // reload counts
  });
});
