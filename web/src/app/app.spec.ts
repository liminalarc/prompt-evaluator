import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { App } from './app';
import { routes } from './app.routes';
import { OrganizationsApiService } from './organizations/organizations-api.service';
import { OrgContextStore } from './shared/org-context.store';

describe('App shell', () => {
  const orgs = [
    { id: 'o1', name: 'Acme' },
    { id: 'o2', name: 'Globex' },
  ];

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        { provide: OrganizationsApiService, useValue: { listOrganizations: () => of(orgs) } },
      ],
    }).compileComponents();
  });

  it('creates the shell', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders Dashboard, Prompts and Analytics nav links (no Home) and a router outlet', () => {
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
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges(); // constructor load() resolves orgs synchronously
    const options = fixture.nativeElement.querySelectorAll('[data-testid="org-select"] option');
    expect(Array.from(options).map((o: any) => o.textContent.trim())).toEqual(
      jasmine.arrayContaining(['Acme', 'Globex']),
    );
  });

  it('switching the org updates the global selection', () => {
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
});
