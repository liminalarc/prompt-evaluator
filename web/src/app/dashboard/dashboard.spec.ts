import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { OrgContextStore } from '../shared/org-context.store';
import { Dashboard } from './dashboard';
import { DashboardFacade } from './dashboard.facade';
import { DashboardView } from './dashboard.model';

const view: DashboardView = {
  prompts: [
    {
      id: 'p1',
      name: 'Alpha',
      versionCount: 2,
      latestTargetModel: 'claude-sonnet-5',
      latestScore: { meanValue: 0.62, passRate: 0.5, runAt: '2026-01-02T00:00:00Z' },
    },
    { id: 'p2', name: 'Beta', versionCount: 0, latestTargetModel: null, latestScore: null },
  ],
  recentRuns: [
    {
      runId: 'r1',
      promptId: 'p1',
      promptName: 'Alpha',
      datasetId: 'd1',
      datasetName: 'DS1',
      createdAt: '2026-01-02T00:00:00Z',
      fixtureCount: 3,
      scoreCount: 6,
    },
  ],
  openRegressions: [
    {
      promptId: 'p1',
      promptName: 'Alpha',
      datasetId: 'd1',
      datasetName: 'DS1',
      scorer: 'LlmJudge (claude)',
      fromVersionNumber: 1,
      toVersionNumber: 2,
      delta: -0.3,
    },
  ],
};

describe('Dashboard', () => {
  function setup(facade: Partial<DashboardFacade>) {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [
        provideRouter([]),
        {
          provide: OrganizationsApiService,
          useValue: { listOrganizations: () => of([{ id: 'o1', name: 'Acme' }]) },
        },
        { provide: DashboardFacade, useValue: facade },
      ],
    });
    TestBed.inject(OrgContextStore).load();
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    return fixture;
  }

  it('renders prompt cards, recent runs and open regressions from the view', () => {
    const fixture = setup({ load: () => of(view) });
    const el = fixture.nativeElement as HTMLElement;

    const cards = el.querySelectorAll('[data-testid="dash-prompt-card"]');
    expect(cards.length).toBe(2);
    expect(cards[0].textContent).toContain('Alpha');
    expect(cards[0].textContent).toContain('0.62'); // latest score
    expect(cards[1].textContent).toContain('No runs'); // unrun prompt

    const runRows = el.querySelectorAll('[data-testid="dash-runs"] tbody tr');
    expect(runRows.length).toBe(1);
    expect(runRows[0].querySelector('a')?.getAttribute('href')).toContain('/eval-runs/r1');

    const regRows = el.querySelectorAll('[data-testid="dash-regression-row"]');
    expect(regRows.length).toBe(1);
    expect(regRows[0].textContent).toContain('LlmJudge');
    expect(regRows[0].textContent).toContain('v1 → v2');
  });

  it('shows empty states when there is no activity', () => {
    const fixture = setup({
      load: () => of({ prompts: view.prompts, recentRuns: [], openRegressions: [] }),
    });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="no-runs"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="no-regressions"]')).toBeTruthy();
  });

  it('shows a loading state until the dashboard resolves', () => {
    const gate = new Subject<DashboardView>();
    const fixture = setup({ load: () => gate.asObservable() });
    expect(fixture.nativeElement.querySelector('[data-testid="loading"]')).toBeTruthy();

    gate.next(view);
    gate.complete();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="loading"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="dash-prompts"]')).toBeTruthy();
  });
});
