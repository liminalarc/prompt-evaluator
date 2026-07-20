import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AiUsageBreakdownRow,
  AiUsageBudget,
  AiUsageCallsPage,
  AiUsageDimension,
  AiUsageFilterState,
  AiUsageMetrics,
  AiUsagePeriod,
  AiUsageTimePoint,
  BudgetStatus,
  CreateBudgetBody,
} from './ai-usage';

/**
 * The single API client for the Admin → AI Usage bounded area (spec 6.1). Every route is
 * `/api/admin/ai-usage/*` and global-admin gated on the server (T4). Components call this, never
 * `HttpClient` directly. The filter is serialized to the shared query params understood by
 * `AiUsageEndpoints` (comma-separated lists; omitted dimensions carry no constraint).
 */
@Injectable({ providedIn: 'root' })
export class AiUsageApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin/ai-usage';

  summary(filter: AiUsageFilterState): Observable<AiUsageMetrics> {
    return this.http.get<AiUsageMetrics>(`${this.base}/summary`, { params: toParams(filter) });
  }

  breakdown(
    filter: AiUsageFilterState,
    dimension: AiUsageDimension,
    topN?: number,
  ): Observable<AiUsageBreakdownRow[]> {
    let params = toParams(filter).set('dimension', dimension);
    if (topN != null) params = params.set('topN', String(topN));
    return this.http.get<AiUsageBreakdownRow[]>(`${this.base}/breakdown`, { params });
  }

  timeSeries(filter: AiUsageFilterState, period: AiUsagePeriod): Observable<AiUsageTimePoint[]> {
    const params = toParams(filter).set('period', period);
    return this.http.get<AiUsageTimePoint[]>(`${this.base}/timeseries`, { params });
  }

  calls(filter: AiUsageFilterState, page: number, pageSize: number): Observable<AiUsageCallsPage> {
    const params = toParams(filter).set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<AiUsageCallsPage>(`${this.base}/calls`, { params });
  }

  /** The current filtered view as CSV text (the caller triggers the download). */
  exportCsv(filter: AiUsageFilterState): Observable<string> {
    return this.http.get(`${this.base}/export.csv`, {
      params: toParams(filter),
      responseType: 'text',
    });
  }

  // Budgets (6.1.T6) — tracking + alerting only.
  listBudgets(): Observable<AiUsageBudget[]> {
    return this.http.get<AiUsageBudget[]>(`${this.base}/budgets`);
  }

  budgetStatus(): Observable<BudgetStatus[]> {
    return this.http.get<BudgetStatus[]>(`${this.base}/budgets/status`);
  }

  createBudget(body: CreateBudgetBody): Observable<AiUsageBudget> {
    return this.http.post<AiUsageBudget>(`${this.base}/budgets`, body);
  }

  deleteBudget(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/budgets/${id}`);
  }
}

/** Serializes the filter to the shared query params (comma-separated lists; omit empty dimensions). */
function toParams(f: AiUsageFilterState): HttpParams {
  let params = new HttpParams();
  if (f.from) params = params.set('from', f.from);
  if (f.to) params = params.set('to', f.to);
  if (f.models.length) params = params.set('models', f.models.join(','));
  if (f.features.length) params = params.set('features', f.features.join(','));
  if (f.statuses.length) params = params.set('statuses', f.statuses.join(','));
  if (f.users.length) params = params.set('users', f.users.join(','));
  if (f.orgs.length) params = params.set('orgs', f.orgs.join(','));
  return params;
}
