import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CaptureTuple, Dataset, DatasetSummary, GenerationGuidance } from '../dataset';

/**
 * The single API client for the datasets bounded area. Components call this, never HttpClient
 * directly. Requests are relative (`/api/...`) so the ng-serve proxy (dev) and nginx (compose)
 * both route them to the .NET API.
 */
@Injectable({ providedIn: 'root' })
export class DatasetsApiService {
  private readonly http = inject(HttpClient);

  listDatasets(): Observable<DatasetSummary[]> {
    return this.http.get<DatasetSummary[]>('/api/datasets');
  }

  getDataset(id: string): Observable<Dataset> {
    return this.http.get<Dataset>(`/api/datasets/${id}`);
  }

  createDataset(name: string, description: string | null): Observable<Dataset> {
    return this.http.post<Dataset>('/api/datasets', { name, description });
  }

  captureFixtures(id: string, tuples: CaptureTuple[]): Observable<Dataset> {
    return this.http.post<Dataset>(`/api/datasets/${id}/fixtures/capture`, { tuples });
  }

  generateFixtures(id: string, guidance: GenerationGuidance, count: number): Observable<Dataset> {
    return this.http.post<Dataset>(`/api/datasets/${id}/fixtures/generate`, { guidance, count });
  }
}
