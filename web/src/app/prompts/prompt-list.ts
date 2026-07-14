import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { Folder } from '../folder';
import { Organization } from '../organization';
import { PromptSummary } from '../prompt';
import { FoldersApiService } from '../folders/folders-api.service';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { PromptsApiService } from './prompts-api.service';

interface Crumb {
  id: string | null; // null = the organization root
  name: string;
}

@Component({
  selector: 'app-prompt-list',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="panel">
      <header class="panel__head">
        <h1 class="title">Prompts</h1>
        <p class="subtitle">
          Pick an organization, then navigate its folders — each prompt keeps its versions,
          datasets, and analytics together.
        </p>
      </header>

      <div class="orgbar">
        <label for="org">Organization</label>
        <select
          id="org"
          data-testid="org-select"
          [ngModel]="selectedOrgId()"
          (ngModelChange)="selectOrg($event)"
        >
          @for (o of organizations(); track o.id) {
            <option [value]="o.id">{{ o.name }}</option>
          }
        </select>
        <button
          class="sb-btn"
          type="button"
          data-testid="toggle-new-org"
          (click)="showNewOrg.set(!showNewOrg())"
        >
          + New org
        </button>
      </div>

      @if (showNewOrg()) {
        <form class="reveal" (submit)="createOrg($event)">
          <div class="sb-field">
            <label for="orgName">New organization name</label>
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
      }

      @if (error(); as message) {
        <div class="error-box" data-testid="error">{{ message }}</div>
      }

      @if (selectedOrgId()) {
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
            class="sb-btn"
            type="button"
            data-testid="toggle-new-folder"
            (click)="showNewFolder.set(!showNewFolder())"
          >
            + New folder
          </button>
          <button
            class="sb-btn"
            type="button"
            data-testid="toggle-new-prompt"
            (click)="showNewPrompt.set(!showNewPrompt())"
          >
            + New prompt
          </button>
        </div>

        @if (showNewFolder()) {
          <form class="reveal" (submit)="createFolder($event)">
            <div class="sb-field">
              <label for="folderName">New folder in {{ currentFolderName() }}</label>
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
        }

        @if (showNewPrompt()) {
          <form class="reveal" (submit)="createPrompt($event)">
            <div class="sb-field">
              <label for="name">New prompt in {{ currentFolderName() }}</label>
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
            <button class="sb-btn sb-btn--primary" type="submit" data-testid="create-prompt">
              Create prompt
            </button>
          </form>
        }

        @if (subfolders().length > 0) {
          <div class="folders-grid" data-testid="subfolders">
            @for (f of subfolders(); track f.id) {
              <button
                class="folder-card"
                type="button"
                [attr.data-testid]="'subfolder-' + f.id"
                (click)="openFolder(f.id)"
              >
                <span class="folder-card__icon">📁</span> {{ f.name }}
              </button>
            }
          </div>
        }

        @if (currentPrompts().length === 0) {
          <p class="empty" data-testid="empty">No prompts in {{ currentFolderName() }}.</p>
        } @else {
          <table class="sb-table" data-testid="prompts">
            <thead>
              <tr>
                <th>Name</th>
                <th>Versions</th>
                <th>Latest target model</th>
                <th>Move to</th>
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
                </tr>
              }
            </tbody>
          </table>
        }
      }
    </section>
  `,
  styleUrl: './prompts.css',
})
export class PromptList implements OnInit {
  private readonly api = inject(PromptsApiService);
  private readonly foldersApi = inject(FoldersApiService);
  private readonly orgsApi = inject(OrganizationsApiService);

  protected readonly organizations = signal<Organization[]>([]);
  protected readonly selectedOrgId = signal<string | null>(null);
  protected readonly folders = signal<Folder[]>([]);
  protected readonly prompts = signal<PromptSummary[]>([]);
  protected readonly currentFolderId = signal<string | null>(null);

  protected readonly error = signal<string | null>(null);
  protected readonly showNewOrg = signal(false);
  protected readonly showNewFolder = signal(false);
  protected readonly showNewPrompt = signal(false);
  protected readonly orgName = signal('');
  protected readonly folderName = signal('');
  protected readonly name = signal('');
  protected readonly description = signal('');

  protected readonly currentOrgName = computed(
    () => this.organizations().find((o) => o.id === this.selectedOrgId())?.name ?? 'Organization',
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

  ngOnInit(): void {
    this.orgsApi.listOrganizations().subscribe({
      next: (orgs) => {
        this.organizations.set(orgs);
        if (orgs.length > 0) {
          this.selectOrg(orgs[0].id);
        }
      },
      error: () => this.error.set('Could not load organizations — is the stack running?'),
    });
  }

  private loadOrgData(orgId: string): void {
    forkJoin({
      folders: this.foldersApi.listFolders(orgId),
      prompts: this.api.listPromptsByOrganization(orgId),
    }).subscribe({
      next: ({ folders, prompts }) => {
        this.folders.set(folders);
        this.prompts.set(prompts);
      },
      error: () => this.error.set('Could not load the organization’s prompts.'),
    });
  }

  protected selectOrg(orgId: string): void {
    this.selectedOrgId.set(orgId);
    this.currentFolderId.set(null);
    this.loadOrgData(orgId);
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
        this.organizations.update((list) => [...list, created]);
        this.selectOrg(created.id);
      },
      error: () => this.error.set('Could not create the organization.'),
    });
  }

  protected createFolder(event: Event): void {
    event.preventDefault();
    const orgId = this.selectedOrgId();
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
    const orgId = this.selectedOrgId();
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
        if (targetFolder) {
          this.api.movePrompt(created.id, targetFolder).subscribe({
            next: () => this.loadOrgData(orgId),
            error: () => this.error.set('Prompt created, but could not file it into the folder.'),
          });
        } else {
          this.loadOrgData(orgId);
        }
      },
      error: () => this.error.set('Could not create the prompt.'),
    });
  }

  protected move(prompt: PromptSummary, folderIdValue: string): void {
    const orgId = this.selectedOrgId();
    const folderId = folderIdValue || null;
    if (!orgId || (prompt.folderId ?? null) === folderId) return;
    this.error.set(null);
    this.api.movePrompt(prompt.id, folderId).subscribe({
      next: () => this.loadOrgData(orgId),
      error: () => this.error.set('Could not move the prompt.'),
    });
  }
}
