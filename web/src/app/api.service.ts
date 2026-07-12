import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { EchoResult } from './echo';

/**
 * The single API client for the walking-skeleton seam check. Components call this, never
 * HttpClient directly. Requests are relative (`/api/...`) so the ng-serve proxy (dev) and
 * nginx (compose) both route them to the .NET API.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  echo(prompt: string): Observable<EchoResult> {
    return this.http.post<EchoResult>('/api/echo', { prompt });
  }
}
