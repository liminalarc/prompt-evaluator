import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { UserAdmin } from './user-admin';

describe('UserAdmin (admin user & access management)', () => {
  let httpMock: HttpTestingController;

  const users = [
    {
      id: 'u1',
      email: 'admin@test.local',
      displayName: 'Admin',
      isAdmin: true,
      memberships: [{ organizationId: 'o1', role: 'Owner' }],
    },
    {
      id: 'u2',
      email: 'member@test.local',
      displayName: 'Member',
      isAdmin: false,
      memberships: [],
    },
  ];
  const orgs = [{ id: 'o1', name: 'Acme' }];

  function setup() {
    TestBed.configureTestingModule({
      imports: [UserAdmin],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(UserAdmin);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges(); // ngOnInit → load users + orgs
    httpMock.expectOne('/api/admin/users').flush(users);
    httpMock.expectOne('/api/organizations').flush(orgs);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => httpMock.verify());

  it('lists users with email and admin status', () => {
    const fixture = setup();
    const table = fixture.nativeElement.querySelector('[data-testid="users-admin-table"]');
    expect(table.textContent).toContain('admin@test.local');
    expect(table.textContent).toContain('member@test.local');
    expect(table.textContent).toContain('Acme'); // membership org resolved to its name
  });

  it('creates a user, posting email/name/password then reloading the list (4.6)', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      newEmail: { set: (v: string) => void };
      newDisplayName: { set: (v: string) => void };
      newPassword: { set: (v: string) => void };
      createUser: () => void;
    };
    cmp.newEmail.set('created@test.local');
    cmp.newDisplayName.set('Created');
    cmp.newPassword.set('Created-Pass-1');
    cmp.createUser();

    const req = httpMock.expectOne('/api/admin/users');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      email: 'created@test.local',
      displayName: 'Created',
      password: 'Created-Pass-1',
    });
    req.flush({
      id: 'u3',
      email: 'created@test.local',
      displayName: 'Created',
      isAdmin: false,
      memberships: [],
    });
    httpMock.expectOne('/api/admin/users').flush(users); // reload
  });

  it('shows an inline error when required create fields are missing (4.6)', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      toggleCreate: () => void;
      createUser: () => void;
    };
    cmp.toggleCreate(); // open the form
    cmp.createUser(); // all fields empty → no HTTP call, inline error
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="create-user-error"]')).toBeTruthy();
    // httpMock.verify() in afterEach asserts no create request was made
  });

  it('toggles global-admin, posting the new flag then reloading', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as { toggleAdmin: (u: unknown) => void };
    cmp.toggleAdmin(users[1]); // member → grant admin

    const req = httpMock.expectOne('/api/admin/users/u2/admin');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ isAdmin: true });
    req.flush(null);
    httpMock.expectOne('/api/admin/users').flush(users); // reload
  });

  it('grants an org membership with a role, then reloads', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      setAddOrg: (id: string, v: string) => void;
      setAddRole: (id: string, v: string) => void;
      addMembership: (u: unknown) => void;
    };
    cmp.setAddOrg('u2', 'o1');
    cmp.setAddRole('u2', 'Member');
    cmp.addMembership(users[1]);

    const req = httpMock.expectOne('/api/admin/users/u2/organizations');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ organizationId: 'o1', role: 'Member' });
    req.flush(null);
    httpMock.expectOne('/api/admin/users').flush(users); // reload
  });

  it('revokes an org membership, then reloads', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      revokeMembership: (u: unknown, orgId: string) => void;
    };
    cmp.revokeMembership(users[0], 'o1');

    const req = httpMock.expectOne('/api/admin/users/u1/organizations/o1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    httpMock.expectOne('/api/admin/users').flush(users); // reload
  });

  it('sets a new password for a user', () => {
    const fixture = setup();
    const cmp = fixture.componentInstance as unknown as {
      setPwd: (id: string, v: string) => void;
      savePassword: (u: unknown) => void;
    };
    cmp.setPwd('u2', 'New-Password-1');
    cmp.savePassword(users[1]);

    const req = httpMock.expectOne('/api/admin/users/u2/password');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ newPassword: 'New-Password-1' });
    req.flush(null);
  });
});
