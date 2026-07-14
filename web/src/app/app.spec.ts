import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { App } from './app';
import { routes } from './app.routes';
import { AuthService } from './auth/auth.service';
import { AuthUser } from './auth/user';
import { OrganizationsApiService } from './organizations/organizations-api.service';
import { OrgContextStore } from './shared/org-context.store';

/** A minimal signals-based stand-in for AuthService — authenticated by default. */
function fakeAuth(user: AuthUser | null = { id: 'u1', email: 'a@b.co', displayName: 'Ada' }) {
  const currentUser = signal<AuthUser | null>(user);
  return {
    currentUser,
    isAuthenticated: () => currentUser() !== null,
    logout: () => of(void 0),
    clearSession: () => currentUser.set(null),
  };
}

describe('App shell', () => {
  const orgs = [
    { id: 'o1', name: 'Acme' },
    { id: 'o2', name: 'Globex' },
  ];

  function configure(auth = fakeAuth()) {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        { provide: AuthService, useValue: auth },
        { provide: OrganizationsApiService, useValue: { listOrganizations: () => of(orgs) } },
      ],
    });
  }

  it('creates the shell', () => {
    configure();
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders Dashboard, Prompts and Analytics nav links (no Home) and a router outlet', () => {
    configure();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const links = Array.from(compiled.querySelectorAll('a.nav__link')).map((a) =>
      a.textContent?.trim(),
    );
    expect(links).toEqual(jasmine.arrayContaining(['Dashboard', 'Prompts', 'Analytics']));
    expect(links).not.toContain('Home');
    expect(links).not.toContain('Datasets');
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });

  it('renders the global org switcher populated from the store', () => {
    configure();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges(); // auth effect → org.load() resolves orgs synchronously
    const options = fixture.nativeElement.querySelectorAll('[data-testid="org-select"] option');
    expect(Array.from(options).map((o: any) => o.textContent.trim())).toEqual(
      jasmine.arrayContaining(['Acme', 'Globex']),
    );
  });

  it('switching the org updates the global selection', () => {
    configure();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const store = TestBed.inject(OrgContextStore);
    expect(store.currentOrgId()).toBe('o1');

    const select: HTMLSelectElement = fixture.nativeElement.querySelector(
      '[data-testid="org-select"]',
    );
    select.value = 'o2';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(store.currentOrgId()).toBe('o2');
  });

  it('shows the signed-in user and a logout button when authenticated', () => {
    configure();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="current-user"]')?.textContent).toContain('Ada');
    expect(el.querySelector('[data-testid="logout"]')).toBeTruthy();
  });

  it('hides the nav, switcher and user chrome when not authenticated', () => {
    configure(fakeAuth(null));
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a.nav__link')).toBeFalsy();
    expect(el.querySelector('[data-testid="org-select"]')).toBeFalsy();
    expect(el.querySelector('[data-testid="logout"]')).toBeFalsy();
  });
});
