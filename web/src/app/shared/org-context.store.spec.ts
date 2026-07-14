import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { OrgContextStore } from './org-context.store';

const STORAGE_KEY = 'litmus.currentOrgId';
const orgs = [
  { id: 'o1', name: 'Alpha' },
  { id: 'o2', name: 'Beta' },
];

describe('OrgContextStore', () => {
  let store: OrgContextStore;
  let router: Router;

  function configure(list = orgs) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: OrganizationsApiService, useValue: { listOrganizations: () => of(list) } },
      ],
    });
    store = TestBed.inject(OrgContextStore);
    router = TestBed.inject(Router);
  }

  beforeEach(() => localStorage.clear());

  it('defaults to the first org when nothing is remembered', () => {
    configure();
    store.load();
    expect(store.currentOrgId()).toBe('o1');
    expect(store.currentOrg()?.name).toBe('Alpha');
    expect(store.organizations().length).toBe(2);
  });

  it('prefers a valid remembered org from localStorage', () => {
    localStorage.setItem(STORAGE_KEY, 'o2');
    configure();
    store.load();
    expect(store.currentOrgId()).toBe('o2');
  });

  it('ignores a remembered org that no longer exists', () => {
    localStorage.setItem(STORAGE_KEY, 'ghost');
    configure();
    store.load();
    expect(store.currentOrgId()).toBe('o1');
  });

  it('prefers a valid ?org= query param over localStorage', async () => {
    localStorage.setItem(STORAGE_KEY, 'o1');
    configure();
    await router.navigate([], { queryParams: { org: 'o2' } });
    store.load();
    expect(store.currentOrgId()).toBe('o2');
  });

  it('select updates the signal, persists to localStorage, and reflects ?org= in the url', () => {
    configure();
    store.load(); // selects o1
    const nav = spyOn(router, 'navigate');
    store.select('o2');
    expect(store.currentOrgId()).toBe('o2');
    expect(localStorage.getItem(STORAGE_KEY)).toBe('o2');
    expect(nav).toHaveBeenCalledWith(
      [],
      jasmine.objectContaining({ queryParams: { org: 'o2' }, queryParamsHandling: 'merge' }),
    );
  });

  it('add appends a new org and makes it current', () => {
    configure();
    store.load();
    store.add({ id: 'o3', name: 'Gamma' });
    expect(store.organizations().map((o) => o.id)).toContain('o3');
    expect(store.currentOrgId()).toBe('o3');
  });
});
