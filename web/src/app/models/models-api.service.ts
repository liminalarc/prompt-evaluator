import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ModelCatalogEntry } from '../model';

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
}
