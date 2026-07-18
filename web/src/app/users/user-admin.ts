import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Organization } from '../organization';
import {
  Breadcrumb,
  Card,
  Crumb,
  EmptyState,
  ErrorState,
  LoadingState,
  PageHeader,
  StatusBadge,
} from '../shared';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { UserDetail } from './user';
import { UsersApiService } from './users-api.service';

const ROLES = ['Owner', 'Member'];

/**
 * Admin user & access management (spec 4.3, + create-user 4.6). Reached at /admin/users under the
 * Admin folder, gated to global admins. Lets an admin create a user (4.6, no email), toggle
 * global-admin, manage org membership + role, and set a password. Org-entity management is spec 4.4.
 */
@Component({
  selector: 'app-user-admin',
  imports: [
    FormsModule,
    Breadcrumb,
    Card,
    EmptyState,
    ErrorState,
    LoadingState,
    PageHeader,
    StatusBadge,
  ],
  template: `
    <section class="panel panel--wide">
      <app-breadcrumb [items]="crumbs()" />

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      <app-page-header
        heading="Users"
        subtitle="Create users and manage admin access, organization membership, and passwords."
      />

      <app-card heading="Users">
        <div class="create-user">
          @if (showCreate()) {
            <div class="create-form" data-testid="create-user-form">
              <input
                type="email"
                placeholder="Email"
                data-testid="new-user-email"
                name="new-user-email"
                [ngModel]="newEmail()"
                (ngModelChange)="newEmail.set($event)"
              />
              <input
                type="text"
                placeholder="Display name"
                data-testid="new-user-name"
                name="new-user-name"
                [ngModel]="newDisplayName()"
                (ngModelChange)="newDisplayName.set($event)"
              />
              <input
                type="password"
                placeholder="Password"
                data-testid="new-user-password"
                name="new-user-password"
                [ngModel]="newPassword()"
                (ngModelChange)="newPassword.set($event)"
              />
              <button
                type="button"
                class="sb-btn sb-btn--sm sb-btn--primary"
                data-testid="create-user"
                (click)="createUser()"
              >
                Create user
              </button>
              <button
                type="button"
                class="sb-btn sb-btn--sm sb-btn--ghost"
                (click)="toggleCreate()"
              >
                Cancel
              </button>
              @if (createError(); as message) {
                <span class="create-error" data-testid="create-user-error">{{ message }}</span>
              }
            </div>
          } @else {
            <button
              type="button"
              class="sb-btn sb-btn--sm sb-btn--secondary"
              data-testid="toggle-create-user"
              (click)="toggleCreate()"
            >
              + New user
            </button>
          }
        </div>

        @if (loading()) {
          <app-loading-state label="Loading users…" />
        } @else if (users().length === 0) {
          <app-empty-state message="No users yet." />
        } @else {
          <table class="sb-table" data-testid="users-admin-table">
            <thead>
              <tr>
                <th>User</th>
                <th>Admin</th>
                <th>Organizations</th>
                <th>Password</th>
              </tr>
            </thead>
            <tbody>
              @for (u of users(); track u.id) {
                <tr [attr.data-user-email]="u.email">
                  <td>
                    <strong>{{ u.displayName || u.email }}</strong>
                    <div class="muted">{{ u.email }}</div>
                  </td>
                  <td>
                    <label class="admin-toggle">
                      <input
                        type="checkbox"
                        data-testid="admin-toggle"
                        [ngModel]="u.isAdmin"
                        (ngModelChange)="toggleAdmin(u)"
                        [attr.name]="'admin-' + u.id"
                      />
                      <app-status-badge
                        [variant]="u.isAdmin ? 'success' : 'neutral'"
                        [label]="u.isAdmin ? 'Admin' : 'User'"
                      />
                    </label>
                  </td>
                  <td>
                    <div class="memberships">
                      @for (m of u.memberships; track m.organizationId) {
                        <span class="membership-chip">
                          {{ orgName(m.organizationId) }} · {{ m.role }}
                          <button
                            type="button"
                            class="chip-x"
                            data-testid="revoke-membership"
                            (click)="revokeMembership(u, m.organizationId)"
                            aria-label="Remove"
                          >
                            ×
                          </button>
                        </span>
                      }
                    </div>
                    <div class="add-membership">
                      <select
                        data-testid="add-membership-org"
                        [ngModel]="addOrgFor(u.id)"
                        (ngModelChange)="setAddOrg(u.id, $event)"
                        [attr.name]="'org-' + u.id"
                      >
                        <option value="">Add org…</option>
                        @for (o of orgs(); track o.id) {
                          <option [value]="o.id">{{ o.name }}</option>
                        }
                      </select>
                      <select
                        data-testid="add-membership-role"
                        [ngModel]="addRoleFor(u.id)"
                        (ngModelChange)="setAddRole(u.id, $event)"
                        [attr.name]="'role-' + u.id"
                      >
                        @for (r of roles; track r) {
                          <option [value]="r">{{ r }}</option>
                        }
                      </select>
                      <button
                        type="button"
                        class="sb-btn sb-btn--sm sb-btn--secondary"
                        data-testid="add-membership"
                        (click)="addMembership(u)"
                      >
                        Add
                      </button>
                    </div>
                  </td>
                  <td>
                    @if (showPwdFor(u.id)) {
                      <div class="set-password">
                        <input
                          type="password"
                          placeholder="New password"
                          data-testid="new-password"
                          [ngModel]="pwdFor(u.id)"
                          (ngModelChange)="setPwd(u.id, $event)"
                          [attr.name]="'pwd-' + u.id"
                        />
                        <button
                          type="button"
                          class="sb-btn sb-btn--sm sb-btn--primary"
                          data-testid="save-password"
                          (click)="savePassword(u)"
                        >
                          Save
                        </button>
                      </div>
                    } @else {
                      <button
                        type="button"
                        class="sb-btn sb-btn--sm sb-btn--secondary"
                        data-testid="toggle-set-password"
                        (click)="togglePwd(u.id)"
                      >
                        Set password
                      </button>
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
      .create-user {
        margin-bottom: var(--sb-space-md);
      }
      .create-form {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--sb-space-sm);
      }
      .create-error {
        color: var(--sb-danger, #c0392b);
        font-size: var(--sb-type-small-size);
      }
      .admin-toggle,
      .set-password,
      .add-membership {
        display: inline-flex;
        align-items: center;
        gap: var(--sb-space-sm);
      }
      .memberships {
        display: flex;
        flex-wrap: wrap;
        gap: var(--sb-space-xs);
        margin-bottom: var(--sb-space-xs);
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
export class UserAdmin implements OnInit {
  private readonly api = inject(UsersApiService);
  private readonly orgsApi = inject(OrganizationsApiService);

  protected readonly roles = ROLES;
  protected readonly users = signal<UserDetail[]>([]);
  protected readonly orgs = signal<Organization[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // Admin create-user form (4.6).
  protected readonly showCreate = signal(false);
  protected readonly newEmail = signal('');
  protected readonly newDisplayName = signal('');
  protected readonly newPassword = signal('');
  protected readonly createError = signal<string | null>(null);

  private readonly addOrg = signal<Record<string, string>>({});
  private readonly addRole = signal<Record<string, string>>({});
  private readonly pwd = signal<Record<string, string>>({});
  private readonly showPwd = signal<Record<string, boolean>>({});

  private readonly orgNames = computed(() => {
    const map = new Map<string, string>();
    for (const o of this.orgs()) map.set(o.id, o.name);
    return map;
  });

  protected readonly crumbs = computed<Crumb[]>(() => [
    { label: 'Dashboard', link: '/' },
    { label: 'Users' },
  ]);

  ngOnInit(): void {
    this.load();
    this.orgsApi.listOrganizations().subscribe({ next: (o) => this.orgs.set(o) });
  }

  protected orgName(id: string): string {
    return this.orgNames().get(id) ?? id;
  }

  protected addOrgFor(id: string): string {
    return this.addOrg()[id] ?? '';
  }
  protected setAddOrg(id: string, v: string): void {
    this.addOrg.update((m) => ({ ...m, [id]: v }));
  }
  protected addRoleFor(id: string): string {
    return this.addRole()[id] ?? 'Member';
  }
  protected setAddRole(id: string, v: string): void {
    this.addRole.update((m) => ({ ...m, [id]: v }));
  }
  protected pwdFor(id: string): string {
    return this.pwd()[id] ?? '';
  }
  protected setPwd(id: string, v: string): void {
    this.pwd.update((m) => ({ ...m, [id]: v }));
  }
  protected showPwdFor(id: string): boolean {
    return this.showPwd()[id] ?? false;
  }
  protected togglePwd(id: string): void {
    this.showPwd.update((m) => ({ ...m, [id]: !m[id] }));
  }

  private load(): void {
    this.api.listUsers().subscribe({
      next: (u) => {
        this.users.set(u);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load users.');
        this.loading.set(false);
      },
    });
  }

  protected toggleCreate(): void {
    this.showCreate.update((v) => !v);
    this.createError.set(null);
  }

  protected createUser(): void {
    const email = this.newEmail().trim();
    const displayName = this.newDisplayName().trim();
    const password = this.newPassword();
    if (!email || !displayName || !password) {
      this.createError.set('Email, display name, and password are all required.');
      return;
    }
    this.createError.set(null);
    this.api.createUser(email, displayName, password).subscribe({
      next: () => {
        this.newEmail.set('');
        this.newDisplayName.set('');
        this.newPassword.set('');
        this.load(); // new user appears in the list; grant org/role there
      },
      error: () =>
        this.createError.set(
          'Could not create the user — the email may be in use or the password too weak (min 8 chars).',
        ),
    });
  }

  protected toggleAdmin(u: UserDetail): void {
    this.error.set(null);
    this.api.setAdmin(u.id, !u.isAdmin).subscribe({
      next: () => this.load(),
      error: () => {
        this.error.set('Could not change admin access (the last admin cannot be removed).');
        this.load(); // resync the checkbox to the server truth
      },
    });
  }

  protected addMembership(u: UserDetail): void {
    const orgId = this.addOrgFor(u.id);
    if (!orgId) return;
    const role = (this.addRoleFor(u.id) as 'Owner' | 'Member') || 'Member';
    this.error.set(null);
    this.api.grantMembership(u.id, orgId, role).subscribe({
      next: () => {
        this.setAddOrg(u.id, '');
        this.load();
      },
      error: () => this.error.set('Could not add the membership.'),
    });
  }

  protected revokeMembership(u: UserDetail, orgId: string): void {
    this.error.set(null);
    this.api.revokeMembership(u.id, orgId).subscribe({
      next: () => this.load(),
      error: () => this.error.set('Could not remove the membership.'),
    });
  }

  protected savePassword(u: UserDetail): void {
    const newPassword = this.pwdFor(u.id);
    if (!newPassword) return;
    this.error.set(null);
    this.api.setPassword(u.id, newPassword).subscribe({
      next: () => {
        this.setPwd(u.id, '');
        this.togglePwd(u.id);
      },
      error: () => this.error.set('Could not set the password — check the policy (min 8 chars).'),
    });
  }
}
