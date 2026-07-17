import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ModelCatalogEntry, ModelRole } from '../model';

/** Admin write payload for a catalog entry (1.13). */
export interface ModelWriteBody {
  displayName: string;
  provider: string;
  roles: ModelRole[];
  inputPricePerMTokUsd: number | null;
  outputPricePerMTokUsd: number | null;
}

/**
 * The single API client for the Model Catalog bounded area (spec 1.13). Feeds the target-model
 * (prompt-detail) and judge-model (dataset-detail) droplists so people pick valid ids instead of
 * typing them. Components call this, never HttpClient directly.
 */
@Injectable({ providedIn: 'root' })
export class ModelsApiService {
  private readonly http = inject(HttpClient);

  /** The active catalog, feeding the role-filtered droplists. */
  listModels(): Observable<ModelCatalogEntry[]> {
    return this.http.get<ModelCatalogEntry[]>('/api/models');
  }

  /** The full catalog incl. deactivated entries — admin management view (admin-gated). */
  listAllModels(): Observable<ModelCatalogEntry[]> {
    return this.http.get<ModelCatalogEntry[]>('/api/models?includeInactive=true');
  }

  createModel(body: ModelWriteBody & { modelId: string }): Observable<ModelCatalogEntry> {
    return this.http.post<ModelCatalogEntry>('/api/models', body);
  }

  updateModel(id: string, body: ModelWriteBody): Observable<ModelCatalogEntry> {
    return this.http.put<ModelCatalogEntry>(`/api/models/${id}`, body);
  }

  setActive(id: string, active: boolean): Observable<ModelCatalogEntry> {
    return this.http.post<ModelCatalogEntry>(
      `/api/models/${id}/${active ? 'activate' : 'deactivate'}`,
      {},
    );
  }
}
