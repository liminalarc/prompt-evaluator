import { TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CompareDrawer } from './compare-drawer';

const versions = [
  {
    id: 'v1',
    versionNumber: 1,
    content: 'Summarize the thread.',
    targetModel: 'm',
    label: null,
    sourceApp: null,
    createdAt: '',
  },
  {
    id: 'v2',
    versionNumber: 2,
    content: 'Summarize the thread concisely.',
    targetModel: 'm',
    label: null,
    sourceApp: null,
    createdAt: '',
  },
];

const comparison = {
  fromVersionId: 'v1',
  fromVersionNumber: 1,
  fromVersionLabel: null,
  fromRunId: 'r1',
  toVersionId: 'v2',
  toVersionNumber: 2,
  toVersionLabel: null,
  toRunId: 'r2',
  scorers: [
    {
      scorer: { identity: 'j', kind: 'LlmJudge', judgeModel: 'claude' },
      fromMean: 0.8,
      toMean: 0.6,
      delta: -0.2,
      fixtures: [
        {
          fixtureId: 'f1',
          fixtureLabel: 'case A',
          fromValue: 0.8,
          toValue: 0.6,
          delta: -0.2,
          fromRationale: 'clear and complete',
          toRationale: 'dropped a key point',
        },
      ],
    },
  ],
};

@Component({
  imports: [CompareDrawer],
  template: `
    <app-compare-drawer
      [open]="open()"
      promptId="p1"
      [versions]="versions"
      datasetId="d1"
      (closed)="open.set(false)"
    />
  `,
})
class Host {
  open = signal(true);
  versions = versions;
}

// Two versions on *different* subject models (cross-model), plus variance to drive within-noise.
const crossModelVersions = [
  { ...versions[0], targetModel: 'claude-sonnet-4-6' },
  { ...versions[1], targetModel: 'claude-sonnet-5' },
];
const variance = [
  {
    scorer: { identity: 'j', kind: 'LlmJudge', judgeModel: 'claude' },
    versions: [
      {
        promptVersionId: 'v1',
        versionNumber: 1,
        versionLabel: null,
        runCount: 3,
        aggregate: { mean: 0.8, stdDev: 0.2, sampleCount: 3, min: 0.6, max: 0.95 },
        fixtures: [],
      },
      {
        promptVersionId: 'v2',
        versionNumber: 2,
        versionLabel: null,
        runCount: 3,
        aggregate: { mean: 0.6, stdDev: 0.2, sampleCount: 3, min: 0.4, max: 0.8 },
        fixtures: [],
      },
    ],
  },
];

@Component({
  imports: [CompareDrawer],
  template: `
    <app-compare-drawer
      [open]="true"
      promptId="p1"
      [versions]="versions"
      datasetId="d1"
      [variance]="variance"
      (closed)="noop()"
    />
  `,
})
class WarnHost {
  versions = crossModelVersions;
  variance = variance;
  noop() {}
}

describe('CompareDrawer [2.19 W7]', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('seeds From→To to the two latest and shows the content diff by default', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    // Opening seeds the pair → the score comparison is fetched eagerly.
    http.expectOne((r) => r.url === '/api/analytics/comparison').flush(comparison);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="drawer"]')).toBeTruthy();
    // Content tab is active by default: the version-diff renders, not the score table.
    expect(el.querySelector('[data-testid="diff"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="fixture-deltas"]')).toBeFalsy();
  });

  // U23 (2.23) — the To select's rendered selection must reflect toId() (v2), not fall back to the
  // first option (v1). The seeded pair is From=v1 → To=v2; the picker showed v1 in the To box.
  it('renders the To picker on the seeded target version, not the first option [U23]', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/analytics/comparison').flush(comparison);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    const from = el.querySelector('[data-testid="compare-from"]') as HTMLSelectElement;
    const to = el.querySelector('[data-testid="compare-to"]') as HTMLSelectElement;
    expect(from.value).toBe('v1'); // From seeded to the prior version
    expect(to.value).toBe('v2'); // To seeded to the latest — the box must show it
  });

  it('switches to Scores and Rationale tabs off the one From→To pick', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/analytics/comparison').flush(comparison);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    (el.querySelector('[data-testid="compare-tab-scores"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="fixture-deltas"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="aggregate-delta"]')).toBeTruthy();
    // Scores tab has no inline "Why" toggle — rationale is its own tab.
    expect(el.querySelector('[data-testid="rationale-toggle"]')).toBeFalsy();

    (el.querySelector('[data-testid="compare-tab-rationale"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="rationale-diff"]')).toBeTruthy();
    expect(el.textContent).toContain('dropped a key point');
  });

  it('warns on the Scores tab when the versions ran on different subject models [R5]', () => {
    const fixture = TestBed.createComponent(WarnHost);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/analytics/comparison').flush(comparison);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    (el.querySelector('[data-testid="compare-tab-scores"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    const warn = el.querySelector('[data-testid="cross-model-warning"]');
    expect(warn).toBeTruthy();
    expect(warn!.textContent).toContain('claude-sonnet-4-6');
    expect(warn!.textContent).toContain('claude-sonnet-5');
  });

  it('warns when a version delta sits within run-to-run noise [R4/2.14]', () => {
    const fixture = TestBed.createComponent(WarnHost);
    fixture.detectChanges();
    // Δ = -0.2 for the judge scorer; combined spread = 0.2 + 0.2 = 0.4 → within noise.
    http.expectOne((r) => r.url === '/api/analytics/comparison').flush(comparison);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    (el.querySelector('[data-testid="compare-tab-scores"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="within-noise"]')).toBeTruthy();
  });
});
