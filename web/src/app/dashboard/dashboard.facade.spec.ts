import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PromptsApiService } from '../prompts/prompts-api.service';
import { DatasetsApiService } from '../datasets/datasets-api.service';
import { EvalRunsApiService } from '../eval-runs/eval-runs-api.service';
import { AnalyticsApiService } from '../analytics/analytics-api.service';
import { DashboardFacade } from './dashboard.facade';

const prompts = [
  {
    id: 'p1',
    folderId: null,
    name: 'Alpha',
    description: null,
    versionCount: 2,
    latestTargetModel: 'm',
  },
  {
    id: 'p2',
    folderId: null,
    name: 'Beta',
    description: null,
    versionCount: 1,
    latestTargetModel: null,
  },
];
const datasets = [
  {
    id: 'd1',
    promptId: 'p1',
    name: 'DS1',
    description: null,
    fixtureCount: 3,
    capturedCount: 3,
    syntheticCount: 0,
  },
  // belongs to a prompt outside the org → must be filtered out (never fetched)
  {
    id: 'dX',
    promptId: 'pOther',
    name: 'Foreign',
    description: null,
    fixtureCount: 1,
    capturedCount: 1,
    syntheticCount: 0,
  },
];
const runsByDataset: Record<string, any[]> = {
  d1: [
    {
      id: 'r0',
      promptId: 'p1',
      promptVersionId: 'v1',
      createdAt: '2026-01-01T00:00:00Z',
      fixtureCount: 3,
      scoreCount: 6,
    },
    {
      id: 'r1',
      promptId: 'p1',
      promptVersionId: 'v2',
      createdAt: '2026-01-02T00:00:00Z',
      fixtureCount: 3,
      scoreCount: 6,
    },
  ],
};
const trendsByDataset: Record<string, any[]> = {
  d1: [
    {
      scorer: { identity: 'abc', kind: 'LlmJudge', judgeModel: 'claude' },
      points: [
        {
          promptVersionId: 'v1',
          versionNumber: 1,
          versionLabel: null,
          runId: 'r0',
          runAt: '2026-01-01T00:00:00Z',
          meanValue: 0.8,
          passRate: 1,
          fixtureCount: 3,
        },
        {
          promptVersionId: 'v2',
          versionNumber: 2,
          versionLabel: null,
          runId: 'r1',
          runAt: '2026-01-02T00:00:00Z',
          meanValue: 0.6,
          passRate: 0.5,
          fixtureCount: 3,
        },
      ],
    },
  ],
};
const regByDataset: Record<string, any[]> = {
  d1: [
    {
      scorer: { identity: 'abc', kind: 'LlmJudge', judgeModel: 'claude' },
      fromVersionId: 'v1',
      fromVersionNumber: 1,
      fromVersionLabel: null,
      toVersionId: 'v2',
      toVersionNumber: 2,
      toVersionLabel: null,
      priorMean: 0.8,
      currentMean: 0.6,
      delta: -0.2,
      pValue: 0.01,
      pairedFixtureCount: 3,
    },
  ],
};

describe('DashboardFacade', () => {
  function make(datasetList = datasets) {
    TestBed.configureTestingModule({
      providers: [
        { provide: PromptsApiService, useValue: { listPromptsByOrganization: () => of(prompts) } },
        { provide: DatasetsApiService, useValue: { listDatasets: () => of(datasetList) } },
        {
          provide: EvalRunsApiService,
          useValue: { listRuns: (id: string) => of(runsByDataset[id] ?? []) },
        },
        {
          provide: AnalyticsApiService,
          useValue: {
            getRegressions: (_p: string, id: string) => of(regByDataset[id] ?? []),
            getTrends: (_p: string, id: string) => of(trendsByDataset[id] ?? []),
          },
        },
      ],
    });
    return TestBed.inject(DashboardFacade);
  }

  it('assembles prompt cards, newest-first runs, and open regressions for the org', (done) => {
    make()
      .load('o1')
      .subscribe((view) => {
        // prompts: latest score comes from the most recent trend point; unrun prompt has none
        expect(view.prompts.map((p) => p.id)).toEqual(['p1', 'p2']);
        expect(view.prompts[0].latestScore?.meanValue).toBe(0.6);
        expect(view.prompts[0].latestScore?.runAt).toBe('2026-01-02T00:00:00Z');
        expect(view.prompts[1].latestScore).toBeNull();

        // runs newest first, joined to the prompt name
        expect(view.recentRuns.map((r) => r.runId)).toEqual(['r1', 'r0']);
        expect(view.recentRuns[0].promptName).toBe('Alpha');
        expect(view.recentRuns[0].datasetName).toBe('DS1');

        // regressions joined + labeled
        expect(view.openRegressions.length).toBe(1);
        expect(view.openRegressions[0].scorer).toContain('LlmJudge');
        expect(view.openRegressions[0].promptName).toBe('Alpha');
        done();
      });
  });

  it('returns bare prompt cards with no runs/regressions when the org has no datasets', (done) => {
    make([datasets[1]]) // only the foreign dataset → filtered out, so effectively none
      .load('o1')
      .subscribe((view) => {
        expect(view.prompts.length).toBe(2);
        expect(view.prompts.every((p) => p.latestScore === null)).toBeTrue();
        expect(view.recentRuns).toEqual([]);
        expect(view.openRegressions).toEqual([]);
        done();
      });
  });
});
