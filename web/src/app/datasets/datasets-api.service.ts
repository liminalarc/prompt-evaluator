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

  /** The datasets belonging to a prompt (1.7) — its own test sets. */
  listDatasetsByPrompt(promptId: string): Observable<DatasetSummary[]> {
    return this.http.get<DatasetSummary[]>(`/api/prompts/${promptId}/datasets`);
  }

  getDataset(id: string): Observable<Dataset> {
    return this.http.get<Dataset>(`/api/datasets/${id}`);
  }

  /** Creates a dataset under its owning prompt (1.7). */
  createDataset(promptId: string, name: string, description: string | null): Observable<Dataset> {
    return this.http.post<Dataset>(`/api/prompts/${promptId}/datasets`, { name, description });
  }

  captureFixtures(id: string, tuples: CaptureTuple[]): Observable<Dataset> {
    return this.http.post<Dataset>(`/api/datasets/${id}/fixtures/capture`, { tuples });
  }

  /** Edits a fixture's editable metadata — label + description (input/origin/seed are fixed). */
  editFixture(
    id: string,
    fixtureId: string,
    label: string | null,
    description: string | null,
  ): Observable<Dataset> {
    return this.http.patch<Dataset>(`/api/datasets/${id}/fixtures/${fixtureId}`, {
      label,
      description,
    });
  }

  generateFixtures(id: string, guidance: GenerationGuidance, count: number): Observable<Dataset> {
    return this.http.post<Dataset>(`/api/datasets/${id}/fixtures/generate`, { guidance, count });
  }

  /** Deletes a dataset and everything scoped to it — fixtures, scorers, runs, scores (1.10). */
  deleteDataset(id: string): Observable<void> {
    return this.http.delete<void>(`/api/datasets/${id}`);
  }
}
