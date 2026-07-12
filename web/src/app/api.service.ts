import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { EvalRun } from './eval-run';

/**
 * The single API client for the eval-run bounded area. Components call this, never
 * HttpClient directly. Requests are relative (`/api/...`) so the ng-serve proxy (dev)
 * and nginx (compose) both route them to the .NET API.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  createEvalRun(prompt: string): Observable<EvalRun> {
    return this.http.post<EvalRun>('/api/eval-runs', { prompt });
  }
}
