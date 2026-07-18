import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable, catchError, concatMap, forkJoin, from, map, of, toArray } from 'rxjs';
import { Folder } from '../folder';
import { PromptSummary } from '../prompt';
import { FoldersApiService } from '../folders/folders-api.service';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { OrgContextStore } from '../shared/org-context.store';
import { Card, ConfirmService, EmptyState, ErrorState, PageHeader } from '../shared';
import { PromptsApiService } from './prompts-api.service';
import { BulkPrompt, parseBulkImport } from './bulk-import';

interface Crumb {
  id: string | null; // null = the organization root
  name: string;
}

/** One row of the bulk-import report — a prompt that succeeded or failed on its own. */
interface ImportRowResult {
  name: string;
  ok: boolean;
  message: string;
}

@Component({
  selector: 'app-prompt-list',
  imports: [FormsModule, RouterLink, Card, EmptyState, ErrorState, PageHeader],
  template: `
    <section class="panel panel--wide">
      <app-page-header
        heading="Prompts"
        subtitle="Navigate this organization's folders — each prompt keeps its versions, datasets, and analytics together. Switch organizations from the top bar."
      >
        <button
          actions
          class="sb-btn sb-btn--secondary"
          type="button"
          data-testid="toggle-new-org"
          (click)="showNewOrg.set(!showNewOrg())"
        >
          + New org
        </button>
        @if (currentOrgId()) {
          <button
            actions
            class="sb-btn sb-btn--danger sb-btn--sm"
            type="button"
            data-testid="delete-org"
            (click)="deleteOrg()"
          >
            Delete org
          </button>
        }
      </app-page-header>

      @if (showNewOrg()) {
        <app-card heading="New organization">
          <form class="form-stack" (submit)="createOrg($event)">
            <div class="sb-field">
              <label for="orgName">Organization name</label>
              <input
                id="orgName"
                name="orgName"
                [ngModel]="orgName()"
                (ngModelChange)="orgName.set($event)"
              />
            </div>
            <button class="sb-btn sb-btn--primary" type="submit" data-testid="create-org">
              Create
            </button>
          </form>
        </app-card>
      }

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      @if (currentOrgId()) {
        <nav class="breadcrumb" data-testid="breadcrumb">
          @for (c of breadcrumb(); track c.id; let last = $last) {
            <button class="crumb" type="button" [disabled]="last" (click)="navigateTo(c.id)">
              {{ c.name }}
            </button>
            @if (!last) {
              <span class="crumb__sep">/</span>
            }
          }
        </nav>

        <div class="toolbar">
          <button
            class="sb-btn sb-btn--secondary"
            type="button"
            data-testid="toggle-new-folder"
            (click)="showNewFolder.set(!showNewFolder())"
          >
            + New folder
          </button>
          <button
            class="sb-btn sb-btn--secondary"
            type="button"
            data-testid="toggle-new-prompt"
            (click)="showNewPrompt.set(!showNewPrompt())"
          >
            + New prompt
          </button>
          <button
            class="sb-btn sb-btn--secondary"
            type="button"
            data-testid="toggle-import"
            (click)="showImport.set(!showImport())"
          >
            + Import prompts
          </button>
        </div>

        @if (showImport()) {
          <app-card heading="Import prompts from a file">
            <p class="subtitle">
              Pick a JSON file — an array of prompts, each with an optional description and a
              <code>versions</code> list. They import into {{ currentFolderName() }}.
            </p>
            <div class="sb-field">
              <label for="importFile">Prompts JSON</label>
              <input
                id="importFile"
                type="file"
                accept=".json,application/json"
                data-testid="import-file"
                [disabled]="importing()"
                (change)="importPrompts($event)"
              />
            </div>
            @if (importResults().length > 0) {
              <table class="sb-table" data-testid="import-results">
                <thead>
                  <tr>
                    <th>Prompt</th>
                    <th>Result</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of importResults(); track $index) {
                    <tr [attr.data-testid]="r.ok ? 'import-ok' : 'import-error'">
                      <td>{{ r.name }}</td>
                      <td>{{ r.message }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </app-card>
        }

        @if (showNewFolder()) {
          <app-card heading="New folder">
            <form class="form-stack" (submit)="createFolder($event)">
              <div class="sb-field">
                <label for="folderName">Folder name in {{ currentFolderName() }}</label>
                <input
                  id="folderName"
                  name="folderName"
                  [ngModel]="folderName()"
                  (ngModelChange)="folderName.set($event)"
                />
              </div>
              <button class="sb-btn sb-btn--primary" type="submit" data-testid="create-folder">
                Add folder
              </button>
            </form>
          </app-card>
        }

        @if (showNewPrompt()) {
          <app-card heading="New prompt">
            <form class="form-stack" (submit)="createPrompt($event)">
              <div class="sb-field">
                <label for="name">Prompt name in {{ currentFolderName() }}</label>
                <input
                  id="name"
                  name="name"
                  [ngModel]="name()"
                  (ngModelChange)="name.set($event)"
                />
              </div>
              <div class="sb-field">
                <label for="description">Description (optional)</label>
                <input
                  id="description"
                  name="description"
                  [ngModel]="description()"
                  (ngModelChange)="description.set($event)"
                />
              </div>
              <button class="sb-btn sb-btn--primary" type="submit" data-testid="create-prompt">
                Create prompt
              </button>
            </form>
          </app-card>
        }

        @if (subfolders().length > 0) {
          <div class="folders-grid" data-testid="subfolders">
            @for (f of subfolders(); track f.id) {
              <div class="folder-tile">
                <button
                  class="folder-card sb-card"
                  type="button"
                  [attr.data-testid]="'subfolder-' + f.id"
                  (click)="openFolder(f.id)"
                >
                  <span class="folder-card__badge" aria-hidden="true">
                    <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor">
                      <path
                        d="M10 4H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-8l-2-2z"
                      />
                    </svg>
                  </span>
                  <span class="folder-card__name">{{ f.name }}</span>
                </button>
                <button
                  class="folder-tile__delete sb-btn sb-btn--ghost sb-btn--sm"
                  type="button"
                  [attr.data-testid]="'delete-folder-' + f.id"
                  [attr.aria-label]="'Delete folder ' + f.name"
                  (click)="deleteFolder(f)"
                >
                  Delete
                </button>
              </div>
            }
          </div>
        }

        <app-card heading="Prompts">
          @if (currentPrompts().length === 0) {
            <app-empty-state
              [message]="'No prompts in ' + currentFolderName() + '.'"
              data-testid="empty"
            />
          } @else {
            <table class="sb-table" data-testid="prompts">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Versions</th>
                  <th>Latest target model</th>
                  <th>Move to</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (p of currentPrompts(); track p.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/prompts', p.id]">{{ p.name }}</a>
                    </td>
                    <td>{{ p.versionCount }}</td>
                    <td>{{ p.latestTargetModel ?? '—' }}</td>
                    <td>
                      <select
                        [ngModel]="p.folderId ?? ''"
                        [attr.name]="'move-' + p.id"
                        [attr.data-testid]="'move-' + p.id"
                        (ngModelChange)="move(p, $event)"
                      >
                        <option value="">{{ currentOrgName() }} (root)</option>
                        @for (f of folders(); track f.id) {
                          <option [value]="f.id">{{ f.name }}</option>
                        }
                      </select>
                    </td>
                    <td>
                      <button
                        class="sb-btn sb-btn--ghost sb-btn--sm"
                        type="button"
                        [attr.data-testid]="'delete-prompt-' + p.id"
                        (click)="deletePrompt(p)"
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </app-card>
      }
    </section>
  `,
  styleUrl: './prompts.css',
  styles: [
    `
      .breadcrumb {
        display: flex;
        align-items: center;
        gap: var(--sb-space-xs);
        flex-wrap: wrap;
        margin: 0;
        font-size: var(--sb-type-small-size);
      }
      .crumb {
        border: none;
        background: transparent;
        padding: 2px 4px;
        border-radius: var(--sb-radius-sm);
        color: var(--sb-primary);
        cursor: pointer;
        font-size: inherit;
      }
      .crumb:disabled {
        color: var(--sb-text);
        cursor: default;
        font-weight: 600;
      }
      .crumb__sep {
        color: var(--sb-text-muted);
      }
      .folders-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
        gap: var(--sb-space-md);
      }
      .folder-tile {
        position: relative;
      }
      .folder-tile__delete {
        position: absolute;
        top: var(--sb-space-xs);
        right: var(--sb-space-xs);
        opacity: 0;
        transition: opacity 0.15s ease;
      }
      .folder-tile:hover .folder-tile__delete,
      .folder-tile:focus-within .folder-tile__delete {
        opacity: 1;
      }
      .folder-card {
        display: flex;
        width: 100%;
        align-items: center;
        gap: var(--sb-space-sm);
        text-align: left;
        padding: var(--sb-space-md) var(--sb-space-lg);
        color: var(--sb-text);
        font-size: var(--sb-type-body-size);
        cursor: pointer;
        transition:
          border-color 0.15s ease,
          box-shadow 0.15s ease;
      }
      .folder-card:hover {
        border-color: var(--sb-primary);
      }
      .folder-card__badge {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 34px;
        height: 34px;
        flex: none;
        border-radius: var(--sb-radius-md);
        background: var(--sb-primary-surface);
        color: var(--sb-primary);
      }
      .folder-card__name {
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
    `,
  ],
})
export class PromptList {
  private readonly api = inject(PromptsApiService);
  private readonly foldersApi = inject(FoldersApiService);
  private readonly orgsApi = inject(OrganizationsApiService);
  private readonly orgStore = inject(OrgContextStore);
  private readonly confirm = inject(ConfirmService);
  private readonly router = inject(Router);

  protected readonly folders = signal<Folder[]>([]);
  protected readonly prompts = signal<PromptSummary[]>([]);
  protected readonly currentFolderId = signal<string | null>(null);

  protected readonly error = signal<string | null>(null);
  protected readonly showNewOrg = signal(false);
  protected readonly showNewFolder = signal(false);
  protected readonly showNewPrompt = signal(false);
  protected readonly showImport = signal(false);

  // Bulk import (1.6): per-row report + an in-flight flag while the POSTs loop.
  protected readonly importResults = signal<ImportRowResult[]>([]);
  protected readonly importing = signal(false);
  protected readonly orgName = signal('');
  protected readonly folderName = signal('');
  protected readonly name = signal('');
  protected readonly description = signal('');

  /** The active org comes from the global context (topbar switcher), not a local picker. */
  protected readonly currentOrgId = this.orgStore.currentOrgId;
  protected readonly currentOrgName = computed(
    () => this.orgStore.currentOrg()?.name ?? 'Organization',
  );

  protected readonly subfolders = computed(() =>
    this.folders().filter((f) => (f.parentId ?? null) === this.currentFolderId()),
  );

  protected readonly currentPrompts = computed(() =>
    this.prompts().filter((p) => (p.folderId ?? null) === this.currentFolderId()),
  );

  protected readonly currentFolderName = computed(() => {
    const id = this.currentFolderId();
    return id
      ? (this.folders().find((f) => f.id === id)?.name ?? 'this folder')
      : this.currentOrgName();
  });

  protected readonly breadcrumb = computed<Crumb[]>(() => {
    const byId = new Map(this.folders().map((f) => [f.id, f]));
    const chain: Crumb[] = [];
    let current = this.currentFolderId();
    while (current) {
      const f = byId.get(current);
      if (!f) break;
      chain.unshift({ id: f.id, name: f.name });
      current = f.parentId;
    }
    return [{ id: null, name: this.currentOrgName() }, ...chain];
  });

  constructor() {
    // Reload the folder tree + prompts whenever the global org changes; reset to the org root.
    effect(() => {
      const orgId = this.orgStore.currentOrgId();
      this.currentFolderId.set(null);
      this.folders.set([]);
      this.prompts.set([]);
      if (orgId) {
        this.loadOrgData(orgId);
      }
    });
  }

  private loadOrgData(orgId: string): void {
    forkJoin({
      folders: this.foldersApi.listFolders(orgId),
      prompts: this.api.listPromptsByOrganization(orgId),
    }).subscribe({
      next: ({ folders, prompts }) => {
        // Drop a response for an org we've since switched away from: a slower in-flight request
        // for the previous org must not overwrite the current org's data (stale-response race).
        if (this.orgStore.currentOrgId() !== orgId) return;
        this.folders.set(folders);
        this.prompts.set(prompts);
      },
      error: () => {
        if (this.orgStore.currentOrgId() !== orgId) return;
        this.error.set('Could not load the organization’s prompts.');
      },
    });
  }

  protected openFolder(id: string): void {
    this.currentFolderId.set(id);
  }

  protected navigateTo(id: string | null): void {
    this.currentFolderId.set(id);
  }

  protected createOrg(event: Event): void {
    event.preventDefault();
    const name = this.orgName().trim();
    if (!name) return;
    this.error.set(null);
    this.orgsApi.createOrganization(name).subscribe({
      next: (created) => {
        this.orgName.set('');
        this.showNewOrg.set(false);
        this.orgStore.add(created); // append + make current → effect reloads the tree
      },
      error: () => this.error.set('Could not create the organization.'),
    });
  }

  protected createFolder(event: Event): void {
    event.preventDefault();
    const orgId = this.currentOrgId();
    const name = this.folderName().trim();
    if (!orgId || !name) return;
    this.error.set(null);
    this.foldersApi.createFolder(orgId, name, this.currentFolderId()).subscribe({
      next: () => {
        this.folderName.set('');
        this.showNewFolder.set(false);
        this.loadOrgData(orgId);
      },
      error: () => this.error.set('Could not create the folder.'),
    });
  }

  protected createPrompt(event: Event): void {
    event.preventDefault();
    const orgId = this.currentOrgId();
    const name = this.name().trim();
    if (!orgId || !name) return;
    this.error.set(null);
    const description = this.description().trim() || null;
    const targetFolder = this.currentFolderId();
    this.api.createPrompt(orgId, name, description).subscribe({
      next: (created) => {
        this.name.set('');
        this.description.set('');
        this.showNewPrompt.set(false);
        // Navigate to the thing you just made (U1) — the new prompt's workspace. File it into the
        // current folder first when one is active, so the workspace opens already filed.
        if (targetFolder) {
          this.api.movePrompt(created.id, targetFolder).subscribe({
            next: () => void this.router.navigate(['/prompts', created.id]),
            error: () => this.error.set('Prompt created, but could not file it into the folder.'),
          });
        } else {
          void this.router.navigate(['/prompts', created.id]);
        }
      },
      error: () => this.error.set('Could not create the prompt.'),
    });
  }

  /**
   * Bulk import (1.6): read a JSON file, then orchestrate the import client-side by looping the
   * existing create/add-version POSTs into the current org + folder. No new API endpoint.
   */
  protected importPrompts(event: Event): void {
    const orgId = this.currentOrgId();
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!orgId || !file) return;
    input.value = ''; // allow re-picking the same file after a fix

    const reader = new FileReader();
    reader.onload = () => this.runBulkImport(orgId, String(reader.result ?? ''));
    reader.onerror = () => this.error.set('Could not read that file.');
    reader.readAsText(file);
  }

  private runBulkImport(orgId: string, text: string): void {
    const parsed = parseBulkImport(text);
    if (!parsed.ok) {
      this.error.set(parsed.error);
      return;
    }
    this.error.set(null);
    this.importResults.set([]);
    this.importing.set(true);
    const folderId = this.currentFolderId();

    // Sequential: each prompt (and its versions) is created in order so partial failures are
    // reported per row without a transaction — an early failure never stops later prompts.
    from(parsed.prompts)
      .pipe(concatMap((p) => this.importOne(orgId, folderId, p)))
      .subscribe({
        next: (result) => this.importResults.update((rows) => [...rows, result]),
        complete: () => {
          this.importing.set(false);
          this.loadOrgData(orgId); // surface everything that landed
        },
      });
  }

  private importOne(
    orgId: string,
    folderId: string | null,
    p: BulkPrompt,
  ): Observable<ImportRowResult> {
    return this.api.createPrompt(orgId, p.name, p.description).pipe(
      concatMap((created) => {
        const filed$: Observable<unknown> = folderId
          ? this.api.movePrompt(created.id, folderId)
          : of(null);
        const versions$: Observable<unknown> =
          p.versions.length === 0
            ? of(null)
            : from(p.versions).pipe(
                concatMap((v) =>
                  this.api.addVersion(created.id, {
                    content: v.content,
                    targetModel: v.targetModel,
                    label: v.label,
                    sourceApp: null,
                  }),
                ),
                toArray(),
              );
        return filed$.pipe(concatMap(() => versions$));
      }),
      map((): ImportRowResult => ({
        name: p.name,
        ok: true,
        message: `Imported with ${p.versions.length} version(s).`,
      })),
      catchError(() => of<ImportRowResult>({ name: p.name, ok: false, message: 'Import failed.' })),
    );
  }

  protected move(prompt: PromptSummary, folderIdValue: string): void {
    const orgId = this.currentOrgId();
    const folderId = folderIdValue || null;
    if (!orgId || (prompt.folderId ?? null) === folderId) return;
    this.error.set(null);
    this.api.movePrompt(prompt.id, folderId).subscribe({
      next: () => this.loadOrgData(orgId),
      error: () => this.error.set('Could not move the prompt.'),
    });
  }

  protected async deletePrompt(prompt: PromptSummary): Promise<void> {
    const orgId = this.currentOrgId();
    if (!orgId) return;
    const ok = await this.confirm.ask({
      title: 'Delete prompt',
      message:
        `Deletes “${prompt.name}” and its ${prompt.versionCount} version(s), ` +
        `along with its datasets and all their runs and scores. This cannot be undone.`,
      confirmLabel: 'Delete prompt',
    });
    if (!ok) return;
    this.error.set(null);
    this.api.deletePrompt(prompt.id).subscribe({
      next: () => this.loadOrgData(orgId),
      error: () => this.error.set('Could not delete the prompt.'),
    });
  }

  protected async deleteFolder(folder: Folder): Promise<void> {
    const orgId = this.currentOrgId();
    if (!orgId) return;
    const ok = await this.confirm.ask({
      title: 'Delete folder',
      message:
        `Deletes the folder “${folder.name}”. Its subfolders and prompts move up to ` +
        `${this.currentFolderName()} — nothing inside is deleted.`,
      confirmLabel: 'Delete folder',
    });
    if (!ok) return;
    this.error.set(null);
    this.foldersApi.deleteFolder(folder.id).subscribe({
      next: () => this.loadOrgData(orgId),
      error: () => this.error.set('Could not delete the folder.'),
    });
  }

  protected async deleteOrg(): Promise<void> {
    const id = this.currentOrgId();
    if (!id) return;
    const name = this.currentOrgName();
    const ok = await this.confirm.ask({
      title: 'Delete organization',
      message:
        `Deletes “${name}” and everything under it — all its folders, prompts, datasets, ` +
        `and runs. This cannot be undone.`,
      confirmLabel: 'Delete organization',
    });
    if (!ok) return;
    this.error.set(null);
    this.orgsApi.deleteOrganization(id).subscribe({
      next: () => this.orgStore.remove(id), // drop + repoint context → effect reloads
      error: () => this.error.set('Could not delete the organization.'),
    });
  }
}
