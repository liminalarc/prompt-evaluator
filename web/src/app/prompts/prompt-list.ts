import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { Folder, FolderNode, buildFolderTree } from '../folder';
import { PromptSummary } from '../prompt';
import { FoldersApiService } from '../folders/folders-api.service';
import { PromptsApiService } from './prompts-api.service';

interface TreeRow {
  id: string | null; // null = the synthetic Root (unfiled prompts)
  name: string;
  depth: number;
}

@Component({
  selector: 'app-prompt-list',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="panel">
      <header class="panel__head">
        <h1 class="title">Prompts</h1>
        <p class="subtitle">
          Organized into folders — each prompt keeps its versions, datasets, and analytics together.
        </p>
      </header>

      @if (error(); as message) {
        <div class="error-box" data-testid="error">{{ message }}</div>
      }

      <div class="browse">
        <aside class="sidebar" data-testid="sidebar">
          <nav class="tree" data-testid="folder-tree">
            @for (row of treeRows(); track row.id) {
              <button
                type="button"
                class="tree__node"
                [class.tree__node--active]="row.id === selectedFolderId()"
                [style.padding-left.px]="8 + row.depth * 16"
                [attr.data-testid]="row.id ? 'folder-' + row.id : 'folder-root'"
                (click)="selectFolder(row.id)"
              >
                {{ row.name }}
              </button>
            }
          </nav>

          <form class="new-folder" (submit)="createFolder($event)">
            <input
              name="folderName"
              placeholder="New folder"
              [ngModel]="folderName()"
              (ngModelChange)="folderName.set($event)"
              data-testid="folder-name"
            />
            <button class="sb-btn" type="submit" data-testid="create-folder">Add</button>
          </form>
        </aside>

        <div class="main">
          <p class="breadcrumb" data-testid="breadcrumb">{{ breadcrumb() }}</p>

          <form class="create" (submit)="create($event)">
            <div class="sb-field">
              <label for="name">New prompt name</label>
              <input id="name" name="name" [ngModel]="name()" (ngModelChange)="name.set($event)" />
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
            <button class="sb-btn sb-btn--primary" type="submit" data-testid="create">
              Create prompt in {{ selectedFolderName() }}
            </button>
          </form>

          @if (prompts()) {
            @if (visiblePrompts().length === 0) {
              <p class="empty" data-testid="empty">No prompts in {{ selectedFolderName() }}.</p>
            } @else {
              <table class="sb-table" data-testid="prompts">
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Versions</th>
                    <th>Latest target model</th>
                    <th>Folder</th>
                  </tr>
                </thead>
                <tbody>
                  @for (p of visiblePrompts(); track p.id) {
                    <tr>
                      <td><a [routerLink]="['/prompts', p.id]">{{ p.name }}</a></td>
                      <td>{{ p.versionCount }}</td>
                      <td>{{ p.latestTargetModel ?? '—' }}</td>
                      <td>
                        <select
                          [ngModel]="p.folderId ?? ''"
                          [attr.name]="'move-' + p.id"
                          [attr.data-testid]="'move-' + p.id"
                          (ngModelChange)="move(p, $event)"
                        >
                          <option value="">Root</option>
                          @for (f of folders(); track f.id) {
                            <option [value]="f.id">{{ f.name }}</option>
                          }
                        </select>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          }
        </div>
      </div>
    </section>
  `,
  styleUrl: './prompts.css',
})
export class PromptList implements OnInit {
  private readonly api = inject(PromptsApiService);
  private readonly foldersApi = inject(FoldersApiService);

  protected readonly prompts = signal<PromptSummary[] | null>(null);
  protected readonly folders = signal<Folder[]>([]);
  protected readonly selectedFolderId = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly folderName = signal('');

  protected readonly treeRows = computed<TreeRow[]>(() => {
    const rows: TreeRow[] = [{ id: null, name: 'Root', depth: 0 }];
    const walk = (nodes: FolderNode[], depth: number) => {
      for (const n of nodes) {
        rows.push({ id: n.id, name: n.name, depth });
        walk(n.children, depth + 1);
      }
    };
    walk(buildFolderTree(this.folders()), 1);
    return rows;
  });

  protected readonly visiblePrompts = computed(() =>
    (this.prompts() ?? []).filter((p) => (p.folderId ?? null) === this.selectedFolderId()),
  );

  protected readonly selectedFolderName = computed(() => {
    const id = this.selectedFolderId();
    return id ? (this.folders().find((f) => f.id === id)?.name ?? 'Root') : 'Root';
  });

  protected readonly breadcrumb = computed(() => {
    const byId = new Map(this.folders().map((f) => [f.id, f]));
    const names = ['Root'];
    let current = this.selectedFolderId();
    const chain: string[] = [];
    while (current) {
      const f = byId.get(current);
      if (!f) break;
      chain.unshift(f.name);
      current = f.parentId;
    }
    return [...names, ...chain].join(' / ');
  });

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    forkJoin({
      prompts: this.api.listPrompts(),
      folders: this.foldersApi.listFolders(),
    }).subscribe({
      next: ({ prompts, folders }) => {
        this.prompts.set(prompts);
        this.folders.set(folders);
      },
      error: () => this.error.set('Could not load prompts — is the stack running?'),
    });
  }

  protected selectFolder(id: string | null): void {
    this.selectedFolderId.set(id);
  }

  protected createFolder(event: Event): void {
    event.preventDefault();
    const name = this.folderName().trim();
    if (!name) return;
    this.error.set(null);
    this.foldersApi.createFolder(name, this.selectedFolderId()).subscribe({
      next: () => {
        this.folderName.set('');
        this.load();
      },
      error: () => this.error.set('Could not create the folder.'),
    });
  }

  protected create(event: Event): void {
    event.preventDefault();
    const name = this.name().trim();
    if (!name) return;
    this.error.set(null);
    const description = this.description().trim() || null;
    const targetFolder = this.selectedFolderId();
    this.api.createPrompt(name, description).subscribe({
      next: (created) => {
        this.name.set('');
        this.description.set('');
        // New prompts land in the folder you're browsing (unfiled when Root is selected).
        if (targetFolder) {
          this.api.movePrompt(created.id, targetFolder).subscribe({
            next: () => this.load(),
            error: () => this.error.set('Prompt created, but could not file it into the folder.'),
          });
        } else {
          this.load();
        }
      },
      error: () => this.error.set('Could not create the prompt.'),
    });
  }

  protected move(prompt: PromptSummary, folderIdValue: string): void {
    const folderId = folderIdValue || null;
    if ((prompt.folderId ?? null) === folderId) return;
    this.error.set(null);
    this.api.movePrompt(prompt.id, folderId).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Could not move the prompt.'),
    });
  }
}
