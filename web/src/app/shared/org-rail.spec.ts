import { TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { OrgRail } from './org-rail';
import { OrgContextStore } from './org-context.store';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { AuthService } from '../auth/auth.service';

@Component({
  imports: [OrgRail],
  template: `<app-org-rail [collapsed]="collapsed()" (toggleCollapsed)="toggled = toggled + 1" />`,
})
class Host {
  collapsed = signal(false);
  toggled = 0;
}

describe('OrgRail [2.20 W39]', () => {
  const orgs = signal([
    { id: 'o1', name: 'Acme', role: 'Owner' },
    { id: 'o2', name: 'Globex', role: 'Member' },
  ]);
  const currentId = signal('o1');
  const selectSpy = jasmine.createSpy('select');
  const addSpy = jasmine.createSpy('add');
  const createSpy = jasmine
    .createSpy('createOrganization')
    .and.returnValue(of({ id: 'o3', name: 'Initech', role: 'Owner' }));

  function setup() {
    selectSpy.calls.reset();
    addSpy.calls.reset();
    createSpy.calls.reset();
    currentId.set('o1');
    TestBed.configureTestingModule({
      imports: [Host],
      providers: [
        provideRouter([]),
        {
          provide: OrgContextStore,
          useValue: {
            organizations: orgs,
            currentOrgId: currentId,
            currentOrg: signal({ id: 'o1', name: 'Acme', role: 'Owner' }),
            select: selectSpy,
            add: addSpy,
          },
        },
        { provide: OrganizationsApiService, useValue: { createOrganization: createSpy } },
        { provide: AuthService, useValue: { currentUser: signal(null) } },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture;
  }

  it('lists orgs by name and marks the active one; clicking one selects it', () => {
    const fixture = setup();
    const el = fixture.nativeElement as HTMLElement;
    const items = el.querySelectorAll('[data-testid="org-option"]');
    expect(Array.from(items).map((i) => i.textContent?.trim())).toEqual(['Acme', 'Globex']);
    expect(items[0].getAttribute('aria-current')).toBe('true');

    (el.querySelector('[data-testid="org-option"][data-org-id="o2"]') as HTMLButtonElement).click();
    expect(selectSpy).toHaveBeenCalledWith('o2');
  });

  it('collapses to initials and emits the toggle [real-estate]', () => {
    const fixture = setup();
    const el = fixture.nativeElement as HTMLElement;

    // Collapse toggle emits to the shell.
    (el.querySelector('[data-testid="rail-collapse"]') as HTMLButtonElement).click();
    expect(fixture.componentInstance.toggled).toBe(1);

    // When the shell flips the input, items render as initials (names hidden visually).
    fixture.componentInstance.collapsed.set(true);
    fixture.detectChanges();
    const active = el.querySelector('[data-testid="org-option"][data-org-id="o1"]')!;
    expect(active.querySelector('.org-rail__initial')?.textContent?.trim()).toBe('A');
    // The + stays visible (stacked with the expand toggle) while collapsed, but the create form
    // isn't open until you click it.
    expect(el.querySelector('[data-testid="rail-add-org"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="rail-new-org-name"]')).toBeNull();
  });

  it('creates an org from the rail and makes it current', () => {
    const fixture = setup();
    const el = fixture.nativeElement as HTMLElement;

    (el.querySelector('[data-testid="rail-add-org"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="rail-new-org-name"]')).toBeTruthy(); // form revealed

    const rail = fixture.debugElement.query(By.directive(OrgRail)).componentInstance as {
      newOrgName: { set(v: string): void };
      createOrg(e: Event): void;
    };
    rail.newOrgName.set('Initech');
    rail.createOrg(new Event('submit'));

    expect(createSpy).toHaveBeenCalledWith('Initech');
    expect(addSpy).toHaveBeenCalledWith({ id: 'o3', name: 'Initech', role: 'Owner' });
  });

  it('shows the Manage link for the current org owner (4.5)', () => {
    const fixture = setup();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="manage-org"]'),
    ).toBeTruthy();
  });
});
