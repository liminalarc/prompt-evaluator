import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RegressionFlag, TrendSeries, VersionComparison } from '../analytics';

/**
 * The single API client for the analytics bounded area (trends, regressions, comparison).
 * Components call this, never HttpClient directly. Requests are relative (`/api/...`).
 */
@Injectable({ providedIn: 'root' })
export class AnalyticsApiService {
  private readonly http = inject(HttpClient);

  getTrends(promptId: string, datasetId: string): Observable<TrendSeries[]> {
    return this.http.get<TrendSeries[]>('/api/analytics/trends', {
      params: new HttpParams().set('promptId', promptId).set('datasetId', datasetId),
    });
  }

  getRegressions(
    promptId: string,
    datasetId: string,
    threshold?: number,
  ): Observable<RegressionFlag[]> {
    let params = new HttpParams().set('promptId', promptId).set('datasetId', datasetId);
    if (threshold != null) {
      params = params.set('threshold', threshold);
    }
    return this.http.get<RegressionFlag[]>('/api/analytics/regressions', { params });
  }

  getComparison(
    promptId: string,
    datasetId: string,
    fromVersionId: string,
    toVersionId: string,
  ): Observable<VersionComparison> {
    return this.http.get<VersionComparison>('/api/analytics/comparison', {
      params: new HttpParams()
        .set('promptId', promptId)
        .set('datasetId', datasetId)
        .set('fromVersionId', fromVersionId)
        .set('toVersionId', toVersionId),
    });
  }
}
