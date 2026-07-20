import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BackportArtifact, Prompt, PromptSummary, PromptVersionStatus } from '../prompt';

export interface AddVersionBody {
  content: string;
  targetModel: string;
  label: string | null;
  sourceApp: string | null;
}

/**
 * The single API client for the prompt-registry bounded area. Components call this, never
 * HttpClient directly. Requests are relative (`/api/...`) so the ng-serve proxy (dev) and
 * nginx (compose) both route them to the .NET API.
 */
@Injectable({ providedIn: 'root' })
export class PromptsApiService {
  private readonly http = inject(HttpClient);

  listPrompts(): Observable<PromptSummary[]> {
    return this.http.get<PromptSummary[]>('/api/prompts');
  }

  /** The prompts in an organization (1.9). */
  listPromptsByOrganization(organizationId: string): Observable<PromptSummary[]> {
    return this.http.get<PromptSummary[]>(`/api/organizations/${organizationId}/prompts`);
  }

  getPrompt(id: string): Observable<Prompt> {
    return this.http.get<Prompt>(`/api/prompts/${id}`);
  }

  /** Creates a prompt under an organization (1.9). */
  createPrompt(
    organizationId: string,
    name: string,
    description: string | null,
  ): Observable<Prompt> {
    return this.http.post<Prompt>(`/api/organizations/${organizationId}/prompts`, {
      name,
      description,
    });
  }

  addVersion(id: string, body: AddVersionBody): Observable<Prompt> {
    return this.http.post<Prompt>(`/api/prompts/${id}/versions`, body);
  }

  /** Edits a version's editable metadata — its label (content + target model are immutable). */
  editVersionLabel(id: string, versionId: string, label: string | null): Observable<Prompt> {
    return this.http.patch<Prompt>(`/api/prompts/${id}/versions/${versionId}`, { label });
  }

  /** Moves a prompt into a folder (1.7), or unfiles it to the root when folderId is null. */
  movePrompt(id: string, folderId: string | null): Observable<Prompt> {
    return this.http.post<Prompt>(`/api/prompts/${id}/move`, { folderId });
  }

  /** The derived per-version lifecycle status (1.16): Current / Backport-eligible / Regressed. */
  getVersionStatus(id: string): Observable<PromptVersionStatus> {
    return this.http.get<PromptVersionStatus>(`/api/prompts/${id}/version-status`);
  }

  /**
   * Mark a version "Current in source" (1.16) — also the mark-as-backported action (move Current to a
   * shipped, higher-scoring version). Returns the recomputed status so badges update at once.
   */
  setCurrentVersion(
    id: string,
    versionId: string,
    commitSha: string | null = null,
  ): Observable<PromptVersionStatus> {
    return this.http.post<PromptVersionStatus>(
      `/api/prompts/${id}/versions/${versionId}/set-current`,
      { commitSha },
    );
  }

  /**
   * The generated backport artifact for the prompt's single backport target (1.20): the ready-to-apply
   * exact prompt + downloadable markdown. 404 when the prompt has no target (nothing to ship).
   */
  getBackportArtifact(id: string): Observable<BackportArtifact> {
    return this.http.get<BackportArtifact>(`/api/prompts/${id}/backport-artifact`);
  }

  /** Deletes a prompt and everything it owns — versions, datasets, and their runs/scores (1.10). */
  deletePrompt(id: string): Observable<void> {
    return this.http.delete<void>(`/api/prompts/${id}`);
  }
}
