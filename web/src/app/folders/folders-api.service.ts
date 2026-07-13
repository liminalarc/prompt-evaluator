import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Folder } from '../folder';
import { PromptSummary } from '../prompt';

/**
 * The single API client for the folder-tree bounded area (1.7). Components call this, never
 * HttpClient directly. Requests are relative (`/api/...`) so the ng-serve proxy (dev) and nginx
 * (compose) both route them to the .NET API.
 */
@Injectable({ providedIn: 'root' })
export class FoldersApiService {
  private readonly http = inject(HttpClient);

  /** The whole tree as a flat list — the client assembles it via parentId. */
  listFolders(): Observable<Folder[]> {
    return this.http.get<Folder[]>('/api/folders');
  }

  createFolder(name: string, parentId: string | null): Observable<Folder> {
    return this.http.post<Folder>('/api/folders', { name, parentId });
  }

  renameFolder(id: string, name: string): Observable<Folder> {
    return this.http.put<Folder>(`/api/folders/${id}`, { name });
  }

  moveFolder(id: string, parentId: string | null): Observable<Folder> {
    return this.http.post<Folder>(`/api/folders/${id}/move`, { parentId });
  }

  /** The prompts filed directly in a folder. */
  listFolderPrompts(id: string): Observable<PromptSummary[]> {
    return this.http.get<PromptSummary[]>(`/api/folders/${id}/prompts`);
  }

  /** The unfiled prompts — the contents of the root. */
  listRootPrompts(): Observable<PromptSummary[]> {
    return this.http.get<PromptSummary[]>('/api/folders/root/prompts');
  }
}
