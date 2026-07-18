import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { EvalRun, EvalRunSummary, ScorerConfig } from '../eval-run';

export interface ConfigureScorerBody {
  kind: string;
  config: string | null;
  judgeModel: string | null;
}

/**
 * The single API client for the eval-harness bounded area (scorers + runs). Components call this,
 * never HttpClient directly. Requests are relative (`/api/...`).
 */
@Injectable({ providedIn: 'root' })
export class EvalRunsApiService {
  private readonly http = inject(HttpClient);

  listScorers(datasetId: string): Observable<ScorerConfig[]> {
    return this.http.get<ScorerConfig[]>(`/api/datasets/${datasetId}/scorers`);
  }

  configureScorer(datasetId: string, body: ConfigureScorerBody): Observable<ScorerConfig> {
    return this.http.post<ScorerConfig>(`/api/datasets/${datasetId}/scorers`, body);
  }

  /** Edits a configured scorer in place — replaces its descriptor (U9). */
  reconfigureScorer(
    datasetId: string,
    scorerId: string,
    body: ConfigureScorerBody,
  ): Observable<ScorerConfig> {
    return this.http.put<ScorerConfig>(`/api/datasets/${datasetId}/scorers/${scorerId}`, body);
  }

  /** Removes a scorer from a dataset's set (U9). */
  deleteScorer(datasetId: string, scorerId: string): Observable<void> {
    return this.http.delete<void>(`/api/datasets/${datasetId}/scorers/${scorerId}`);
  }

  listRuns(datasetId: string): Observable<EvalRunSummary[]> {
    return this.http.get<EvalRunSummary[]>(`/api/datasets/${datasetId}/eval-runs`);
  }

  triggerRun(datasetId: string, promptId: string, promptVersionId: string): Observable<EvalRun> {
    return this.http.post<EvalRun>(`/api/datasets/${datasetId}/eval-runs`, {
      promptId,
      promptVersionId,
    });
  }

  getRun(runId: string): Observable<EvalRun> {
    return this.http.get<EvalRun>(`/api/eval-runs/${runId}`);
  }
}
