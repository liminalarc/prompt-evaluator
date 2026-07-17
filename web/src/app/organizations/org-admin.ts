import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Breadcrumb,
  Card,
  Crumb,
  EmptyState,
  ErrorState,
  LoadingState,
  PageHeader,
} from '../shared';
import { UserDetail } from '../users/user';
import { UsersApiService } from '../users/users-api.service';
import { OrganizationAdmin, OrgMember } from './org-admin.model';
import { OrgsAdminApiService } from './orgs-admin-api.service';

const ROLES = ['Owner', 'Member'];

/**
 * Admin organization management (spec 4.4). Reached at /admin/organizations under the Admin folder,
 * gated to global admins. Lists all orgs with member counts; create, rename, delete (cascade, behind
 * a type-the-name-to-confirm guard), and drill into an org's members (list/add/remove). This is a
 * *management* surface — global-admin here grants org management, not access to an org's content.
 * Owner-facing member management on the org's own page is spec 4.5.
 */
@Component({
  selector: 'app-org-admin',
  imports: [FormsModule, Breadcrumb, Card, EmptyState, ErrorState, LoadingState, PageHeader],
  template: `
    <section class="panel panel--wide">
      <app-breadcrumb [items]="crumbs()" />

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      <app-page-header
        heading="Organizations"
        subtitle="Create, rename, and delete organizations, and manage who belongs to each."
      />

      <app-card heading="Organizations">
        <div class="create-org">
          <input
            type="text"
            placeholder="New organization name"
            data-testid="new-org-name"
            [ngModel]="newOrgName()"
            (ngModelChange)="newName($event)"
            name="new-org-name"
          />
          <button
            type="button"
            class="sb-btn sb-btn--sm sb-btn--primary"
            data-testid="create-org"
            (click)="createOrg()"
          >
            Create
          </button>
        </div>

        @if (loading()) {
          <app-loading-state label="Loading organizations…" />
        } @else if (orgs().length === 0) {
          <app-empty-state message="No organizations yet." />
        } @else {
          <table class="sb-table" data-testid="orgs-admin-table">
            <thead>
              <tr>
                <th>Organization</th>
                <th>Members</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (o of orgs(); track o.id) {
                <tr [attr.data-org-id]="o.id">
                  <td>
                    @if (isRenaming(o.id)) {
                      <span class="inline-edit">
                        <input
                          type="text"
                          data-testid="rename-input"
                          [ngModel]="renameFor(o.id)"
                          (ngModelChange)="setRename(o.id, $event)"
                          [attr.name]="'rename-' + o.id"
                        />
                        <button
                          type="button"
                          class="sb-btn sb-btn--sm sb-btn--primary"
                          data-testid="save-rename"
                          (click)="saveRename(o)"
                        >
                          Save
                        </button>
                        <button
                          type="button"
                          class="sb-btn sb-btn--sm sb-btn--secondary"
                          (click)="cancelRename(o.id)"
                        >
                          Cancel
                        </button>
                      </span>
                    } @else {
                      <strong>{{ o.name }}</strong>
                    }
                  </td>
                  <td data-testid="member-count">{{ o.memberCount }}</td>
                  <td>
                    <div class="row-actions">
                      <button
                        type="button"
                        class="sb-btn sb-btn--sm sb-btn--secondary"
                        data-testid="toggle-members"
                        (click)="toggleMembers(o)"
                      >
                        {{ isExpanded(o.id) ? 'Hide members' : 'Members' }}
                      </button>
                      <button
                        type="button"
                        class="sb-btn sb-btn--sm sb-btn--secondary"
                        data-testid="rename-org"
                        (click)="startRename(o)"
                      >
                        Rename
                      </button>
                      <button
                        type="button"
                        class="sb-btn sb-btn--sm sb-btn--danger"
                        data-testid="delete-org"
                        (click)="startDelete(o)"
                      >
                        Delete
                      </button>
                    </div>

                    @if (isDeleting(o.id)) {
                      <div class="confirm-delete" data-testid="confirm-delete-org">
                        <p class="danger-note">
                          This permanently deletes <strong>{{ o.name }}</strong> and all its
                          folders, prompts, datasets, and runs. Type the name to confirm.
                        </p>
                        <input
                          type="text"
                          [placeholder]="o.name"
                          data-testid="delete-confirm-name"
                          [ngModel]="deleteConfirmFor(o.id)"
                          (ngModelChange)="setDeleteConfirm(o.id, $event)"
                          [attr.name]="'del-' + o.id"
                        />
                        <button
                          type="button"
                          class="sb-btn sb-btn--sm sb-btn--danger"
                          data-testid="confirm-delete-org-btn"
                          [disabled]="!canDelete(o)"
                          (click)="deleteOrg(o)"
                        >
                          Delete
                        </button>
                        <button
                          type="button"
                          class="sb-btn sb-btn--sm sb-btn--secondary"
                          (click)="cancelDelete(o.id)"
                        >
                          Cancel
                        </button>
                      </div>
                    }

                    @if (isExpanded(o.id)) {
                      <div class="members" data-testid="members">
                        @for (m of membersFor(o.id); track m.userId) {
                          <span class="membership-chip">
                            {{ m.displayName || m.email }} · {{ m.role }}
                            <button
                              type="button"
                              class="chip-x"
                              data-testid="remove-member"
                              (click)="removeMember(o, m.userId)"
                              aria-label="Remove"
                            >
                              ×
                            </button>
                          </span>
                        } @empty {
                          <span class="muted">No members.</span>
                        }
                        <div class="add-member">
                          <select
                            data-testid="add-member-user"
                            [ngModel]="addUserFor(o.id)"
                            (ngModelChange)="setAddUser(o.id, $event)"
                            [attr.name]="'member-' + o.id"
                          >
                            <option value="">Add user…</option>
                            @for (u of users(); track u.id) {
                              <option [value]="u.id">{{ u.displayName || u.email }}</option>
                            }
                          </select>
                          <select
                            data-testid="add-member-role"
                            [ngModel]="addRoleFor(o.id)"
                            (ngModelChange)="setAddRole(o.id, $event)"
                            [attr.name]="'member-role-' + o.id"
                          >
                            @for (r of roles; track r) {
                              <option [value]="r">{{ r }}</option>
                            }
                          </select>
                          <button
                            type="button"
                            class="sb-btn sb-btn--sm sb-btn--secondary"
                            data-testid="add-member"
                            (click)="addMember(o)"
                          >
                            Add
                          </button>
                        </div>
                      </div>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </app-card>
    </section>
  `,
  styles: [
    `
      .muted {
        font-size: var(--sb-type-small-size);
        color: var(--sb-text-secondary);
      }
      .create-org,
      .inline-edit,
      .row-actions,
      .add-member,
      .confirm-delete {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--sb-space-sm);
      }
      .create-org {
        margin-bottom: var(--sb-space-md);
      }
      .confirm-delete {
        margin-top: var(--sb-space-sm);
      }
      .danger-note {
        flex-basis: 100%;
        margin: 0;
        font-size: var(--sb-type-small-size);
        color: var(--sb-text-secondary);
      }
      .members {
        margin-top: var(--sb-space-sm);
        display: flex;
        flex-wrap: wrap;
        gap: var(--sb-space-xs);
        align-items: center;
      }
      .membership-chip {
        display: inline-flex;
        align-items: center;
        gap: var(--sb-space-xs);
        padding: 0 var(--sb-space-sm);
        border: 1px solid var(--sb-border);
        border-radius: var(--sb-radius-pill, 999px);
        font-size: var(--sb-type-small-size);
      }
      .chip-x {
        border: none;
        background: none;
        cursor: pointer;
        color: var(--sb-text-secondary);
      }
    `,
  ],
})
export class OrgAdmin implements OnInit {
  private readonly api = inject(OrgsAdminApiService);
  private readonly usersApi = inject(UsersApiService);

  protected readonly roles = ROLES;
  protected readonly orgs = signal<OrganizationAdmin[]>([]);
  protected readonly users = signal<UserDetail[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly newOrgName = signal('');
  private readonly renaming = signal<Record<string, boolean>>({});
  private readonly renameValue = signal<Record<string, string>>({});
  private readonly deleting = signal<Record<string, boolean>>({});
  private readonly deleteConfirm = signal<Record<string, string>>({});
  private readonly expanded = signal<Record<string, boolean>>({});
  private readonly members = signal<Record<string, OrgMember[]>>({});
  private readonly addUser = signal<Record<string, string>>({});
  private readonly addRole = signal<Record<string, string>>({});

  protected readonly crumbs = signal<Crumb[]>([
    { label: 'Dashboard', link: '/' },
    { label: 'Organizations' },
  ]);

  ngOnInit(): void {
    this.load();
    // The add-member user picker needs the full user list (admin-gated, same as this page).
    this.usersApi.listUsers().subscribe({ next: (u) => this.users.set(u) });
  }

  private load(): void {
    this.api.listOrganizations().subscribe({
      next: (o) => {
        this.orgs.set(o);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load organizations.');
        this.loading.set(false);
      },
    });
  }

  // --- create ---
  protected newName(v: string): void {
    this.newOrgName.set(v);
  }
  protected createOrg(): void {
    const name = this.newOrgName().trim();
    if (!name) return;
    this.error.set(null);
    this.api.createOrganization(name).subscribe({
      next: () => {
        this.newOrgName.set('');
        this.load();
      },
      error: () => this.error.set('Could not create the organization.'),
    });
  }

  // --- rename ---
  protected isRenaming(id: string): boolean {
    return this.renaming()[id] ?? false;
  }
  protected renameFor(id: string): string {
    return this.renameValue()[id] ?? '';
  }
  protected setRename(id: string, v: string): void {
    this.renameValue.update((m) => ({ ...m, [id]: v }));
  }
  protected startRename(o: OrganizationAdmin): void {
    this.renameValue.update((m) => ({ ...m, [o.id]: o.name }));
    this.renaming.update((m) => ({ ...m, [o.id]: true }));
  }
  protected cancelRename(id: string): void {
    this.renaming.update((m) => ({ ...m, [id]: false }));
  }
  protected saveRename(o: OrganizationAdmin): void {
    const name = this.renameFor(o.id).trim();
    if (!name) return;
    this.error.set(null);
    this.api.renameOrganization(o.id, name).subscribe({
      next: () => {
        this.cancelRename(o.id);
        this.load();
      },
      error: () => this.error.set('Could not rename the organization.'),
    });
  }

  // --- delete (type-the-name-to-confirm) ---
  protected isDeleting(id: string): boolean {
    return this.deleting()[id] ?? false;
  }
  protected deleteConfirmFor(id: string): string {
    return this.deleteConfirm()[id] ?? '';
  }
  protected setDeleteConfirm(id: string, v: string): void {
    this.deleteConfirm.update((m) => ({ ...m, [id]: v }));
  }
  protected startDelete(o: OrganizationAdmin): void {
    this.deleteConfirm.update((m) => ({ ...m, [o.id]: '' }));
    this.deleting.update((m) => ({ ...m, [o.id]: true }));
  }
  protected cancelDelete(id: string): void {
    this.deleting.update((m) => ({ ...m, [id]: false }));
  }
  protected canDelete(o: OrganizationAdmin): boolean {
    return this.deleteConfirmFor(o.id) === o.name;
  }
  protected deleteOrg(o: OrganizationAdmin): void {
    if (!this.canDelete(o)) return;
    this.error.set(null);
    this.api.deleteOrganization(o.id).subscribe({
      next: () => {
        this.cancelDelete(o.id);
        this.load();
      },
      error: () => this.error.set('Could not delete the organization.'),
    });
  }

  // --- members drill-in ---
  protected isExpanded(id: string): boolean {
    return this.expanded()[id] ?? false;
  }
  protected membersFor(id: string): OrgMember[] {
    return this.members()[id] ?? [];
  }
  protected toggleMembers(o: OrganizationAdmin): void {
    const open = !this.isExpanded(o.id);
    this.expanded.update((m) => ({ ...m, [o.id]: open }));
    if (open) this.loadMembers(o.id);
  }
  private loadMembers(id: string): void {
    this.api.listMembers(id).subscribe({
      next: (list) => this.members.update((m) => ({ ...m, [id]: list })),
      error: () => this.error.set('Could not load members.'),
    });
  }
  protected addUserFor(id: string): string {
    return this.addUser()[id] ?? '';
  }
  protected setAddUser(id: string, v: string): void {
    this.addUser.update((m) => ({ ...m, [id]: v }));
  }
  protected addRoleFor(id: string): string {
    return this.addRole()[id] ?? 'Member';
  }
  protected setAddRole(id: string, v: string): void {
    this.addRole.update((m) => ({ ...m, [id]: v }));
  }
  protected addMember(o: OrganizationAdmin): void {
    const userId = this.addUserFor(o.id);
    if (!userId) return;
    const role = (this.addRoleFor(o.id) as 'Owner' | 'Member') || 'Member';
    this.error.set(null);
    this.api.addMember(o.id, userId, role).subscribe({
      next: () => {
        this.setAddUser(o.id, '');
        this.loadMembers(o.id);
        this.load(); // member count changed
      },
      error: () => this.error.set('Could not add the member.'),
    });
  }
  protected removeMember(o: OrganizationAdmin, userId: string): void {
    this.error.set(null);
    this.api.removeMember(o.id, userId).subscribe({
      next: () => {
        this.loadMembers(o.id);
        this.load(); // member count changed
      },
      error: () => this.error.set('Could not remove the member.'),
    });
  }
}
