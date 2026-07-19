import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, map, of, switchMap } from 'rxjs';
import { PromptSummary } from '../prompt';
import { DatasetSummary } from '../dataset';
import { RegressionConfidence, TrendPoint, scorerLabel } from '../analytics';
import { PromptsApiService } from '../prompts/prompts-api.service';
import { DatasetsApiService } from '../datasets/datasets-api.service';
import { EvalRunsApiService } from '../eval-runs/eval-runs-api.service';
import { AnalyticsApiService } from '../analytics/analytics-api.service';
import {
  DashboardPromptCard,
  DashboardRegressionRow,
  DashboardRunRow,
  DashboardView,
} from './dashboard.model';

const RECENT_RUNS_LIMIT = 6;
const REGRESSIONS_LIMIT = 6;

/**
 * Assembles the landing dashboard for the active org from **existing read APIs only** (no new
 * endpoint — see 2.4 decisions). The fan-out is bounded by the org's dataset count: prompts +
 * datasets are fetched once, then runs / regressions / trends are fetched per dataset and folded
 * into one view model. Keeping the orchestration here lets the component stay a dumb renderer.
 */
@Injectable({ providedIn: 'root' })
export class DashboardFacade {
  private readonly promptsApi = inject(PromptsApiService);
  private readonly datasetsApi = inject(DatasetsApiService);
  private readonly runsApi = inject(EvalRunsApiService);
  private readonly analyticsApi = inject(AnalyticsApiService);

  load(orgId: string): Observable<DashboardView> {
    return forkJoin({
      prompts: this.promptsApi.listPromptsByOrganization(orgId),
      allDatasets: this.datasetsApi.listDatasets(),
    }).pipe(
      switchMap(({ prompts, allDatasets }) => {
        const promptById = new Map(prompts.map((p) => [p.id, p] as const));
        const datasets = allDatasets.filter((d) => promptById.has(d.promptId));
        if (datasets.length === 0) {
          return of<DashboardView>({
            prompts: prompts.map((p) => this.bareCard(p)),
            recentRuns: [],
            openRegressions: [],
          });
        }
        return forkJoin(
          datasets.map((dataset) =>
            forkJoin({
              dataset: of(dataset),
              runs: this.runsApi.listRuns(dataset.id),
              regressions: this.analyticsApi.getRegressions(dataset.promptId, dataset.id),
              trends: this.analyticsApi.getTrends(dataset.promptId, dataset.id),
            }),
          ),
        ).pipe(map((perDataset) => this.assemble(prompts, promptById, perDataset)));
      }),
    );
  }

  private assemble(
    prompts: PromptSummary[],
    promptById: Map<string, PromptSummary>,
    perDataset: {
      dataset: DatasetSummary;
      runs: {
        id: string;
        promptId: string;
        createdAt: string;
        fixtureCount: number;
        scoreCount: number;
      }[];
      regressions: {
        scorer: { identity: string; kind: string; judgeModel: string | null };
        fromVersionNumber: number;
        toVersionNumber: number;
        delta: number;
        confidence: RegressionConfidence;
      }[];
      trends: { scorer: { kind: string }; points: TrendPoint[] }[];
    }[],
  ): DashboardView {
    // Latest *meaningful* score per prompt: prefer the LLM-judge series' most recent point, since
    // deterministic scorers (Regex/exact-match) are near-always 1.0 and would show a misleading
    // perfect "1.00" on the card (2.19 W33). Fall back to the overall latest when there's no judge.
    const judgeLatest = new Map<string, TrendPoint>();
    const overallLatest = new Map<string, TrendPoint>();
    for (const { dataset, trends } of perDataset) {
      for (const series of trends) {
        for (const point of series.points) {
          const o = overallLatest.get(dataset.promptId);
          if (!o || point.runAt > o.runAt) overallLatest.set(dataset.promptId, point);
          if (series.scorer?.kind === 'LlmJudge') {
            const j = judgeLatest.get(dataset.promptId);
            if (!j || point.runAt > j.runAt) judgeLatest.set(dataset.promptId, point);
          }
        }
      }
    }

    const promptCards: DashboardPromptCard[] = prompts.map((p) => {
      const point = judgeLatest.get(p.id) ?? overallLatest.get(p.id);
      return {
        ...this.bareCard(p),
        latestScore: point
          ? { meanValue: point.meanValue, passRate: point.passRate, runAt: point.runAt }
          : null,
      };
    });

    const recentRuns: DashboardRunRow[] = perDataset
      .flatMap(({ dataset, runs }) =>
        runs.map((r) => ({
          runId: r.id,
          promptId: r.promptId,
          promptName: promptById.get(r.promptId)?.name ?? '—',
          datasetId: dataset.id,
          datasetName: dataset.name,
          createdAt: r.createdAt,
          fixtureCount: r.fixtureCount,
          scoreCount: r.scoreCount,
        })),
      )
      .sort((a, b) => (a.createdAt < b.createdAt ? 1 : a.createdAt > b.createdAt ? -1 : 0))
      .slice(0, RECENT_RUNS_LIMIT);

    const openRegressions: DashboardRegressionRow[] = perDataset
      .flatMap(({ dataset, regressions }) =>
        // Only confirmed regressions land in the dashboard summary; unverified ("possible") drops
        // are surfaced with their caveat in the Analytics view, not asserted as open regressions.
        regressions
          .filter((f) => f.confidence === 'Confirmed')
          .map((f) => ({
            promptId: dataset.promptId,
            promptName: promptById.get(dataset.promptId)?.name ?? '—',
            datasetId: dataset.id,
            datasetName: dataset.name,
            scorer: scorerLabel(f.scorer),
            fromVersionNumber: f.fromVersionNumber,
            toVersionNumber: f.toVersionNumber,
            delta: f.delta,
          })),
      )
      .sort((a, b) => a.delta - b.delta) // most negative (worst) first
      .slice(0, REGRESSIONS_LIMIT);

    return { prompts: promptCards, recentRuns, openRegressions };
  }

  private bareCard(p: PromptSummary): DashboardPromptCard {
    return {
      id: p.id,
      name: p.name,
      versionCount: p.versionCount,
      latestTargetModel: p.latestTargetModel,
      latestScore: null,
    };
  }
}
