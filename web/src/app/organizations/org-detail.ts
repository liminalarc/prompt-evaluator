import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import {
  Breadcrumb,
  Card,
  Crumb,
  EmptyState,
  ErrorState,
  LoadingState,
  PageHeader,
} from '../shared';
import { OrgRole } from '../users/user';
import { OrgMember } from './org-admin.model';
import { OrganizationsApiService } from './organizations-api.service';

const ROLES: OrgRole[] = ['Owner', 'Member'];

/**
 * The owner-facing organization detail page (spec 4.5). Reached at /organizations/:id via the
 * topbar **Manage** link, shown to a member who is the org's Owner (or a global admin). Lists the
 * org's members and lets an owner add (by email), remove, and change roles — scoped to that one org,
 * without the workspace-admin flag (distinct from the 4.4 admin surface). The server enforces
 * owner-or-admin authoritatively; a plain member sees a permission notice and no controls.
 */
@Component({
  selector: 'app-org-detail',
  imports: [FormsModule, Breadcrumb, Card, EmptyState, ErrorState, LoadingState, PageHeader],
  template: `
    <section class="panel panel--wide">
      <app-breadcrumb [items]="crumbs()" />

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      <app-page-header
        [heading]="orgName() || 'Organization'"
        subtitle="Manage who belongs to this organization and their roles."
      />

      @if (loading()) {
        <app-loading-state label="Loading…" />
      } @else if (!canManage()) {
        <app-card heading="Members">
          <p class="muted" data-testid="no-permission">
            You don’t have permission to manage this organization’s members. Only an owner (or a
            workspace admin) can.
          </p>
        </app-card>
      } @else {
        <app-card heading="Members">
          @if (members().length === 0) {
            <app-empty-state message="No members yet." />
          } @else {
            <table class="sb-table" data-testid="members-table">
              <thead>
                <tr>
                  <th>Member</th>
                  <th>Role</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (m of members(); track m.userId) {
                  <tr [attr.data-user-id]="m.userId">
                    <td>
                      <strong>{{ m.displayName || m.email }}</strong>
                      <span class="muted">{{ m.email }}</span>
                    </td>
                    <td>
                      <select
                        data-testid="member-role"
                        [ngModel]="m.role"
                        (ngModelChange)="setRole(m.userId, $event)"
                        [attr.name]="'role-' + m.userId"
                      >
                        @for (r of roles; track r) {
                          <option [value]="r">{{ r }}</option>
                        }
                      </select>
                    </td>
                    <td>
                      <button
                        type="button"
                        class="sb-btn sb-btn--sm sb-btn--danger"
                        data-testid="remove-member"
                        (click)="removeMember(m.userId)"
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }

          <div class="add-member">
            <input
              type="email"
              placeholder="Add member by email"
              data-testid="add-member-email"
              [ngModel]="addEmail()"
              (ngModelChange)="setAddEmail($event)"
              name="add-member-email"
            />
            <select
              data-testid="add-member-role"
              [ngModel]="addRole()"
              (ngModelChange)="setAddRole($event)"
              name="add-member-role"
            >
              @for (r of roles; track r) {
                <option [value]="r">{{ r }}</option>
              }
            </select>
            <button
              type="button"
              class="sb-btn sb-btn--sm sb-btn--primary"
              data-testid="add-member"
              (click)="addMember()"
            >
              Add
            </button>
          </div>
          <p class="muted hint">
            Members must have a LitmusAI account already (they self-register).
          </p>
        </app-card>
      }
    </section>
  `,
  styles: [
    `
      .muted {
        font-size: var(--sb-type-small-size);
        color: var(--sb-text-secondary);
      }
      td .muted {
        display: block;
      }
      .add-member {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--sb-space-sm);
        margin-top: var(--sb-space-md);
      }
      .hint {
        margin: var(--sb-space-xs) 0 0;
      }
    `,
  ],
})
export class OrgDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(OrganizationsApiService);
  private readonly auth = inject(AuthService);

  protected readonly roles = ROLES;
  private readonly orgId = signal('');
  protected readonly orgName = signal('');
  private readonly myRole = signal<OrgRole | undefined>(undefined);
  protected readonly members = signal<OrgMember[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly addEmail = signal('');
  protected readonly addRole = signal<OrgRole>('Member');

  /** Owner-or-admin — the same gate the server enforces. A plain member gets no controls. */
  protected readonly canManage = computed(
    () => this.myRole() === 'Owner' || !!this.auth.currentUser()?.isAdmin,
  );

  protected readonly crumbs = computed<Crumb[]>(() => [
    { label: 'Dashboard', link: '/' },
    { label: this.orgName() || 'Organization' },
  ]);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.orgId.set(id);
    // The member-scoped org list carries the caller's role — resolve the name + role from it.
    this.api.listOrganizations().subscribe({
      next: (orgs) => {
        const org = orgs.find((o) => o.id === id);
        if (org) {
          this.orgName.set(org.name);
          this.myRole.set(org.role);
        }
        if (this.canManage()) {
          this.loadMembers();
        } else {
          this.loading.set(false);
        }
      },
      error: () => {
        this.error.set('Could not load the organization.');
        this.loading.set(false);
      },
    });
  }

  private loadMembers(): void {
    this.api.listMembers(this.orgId()).subscribe({
      next: (list) => {
        this.members.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load members.');
        this.loading.set(false);
      },
    });
  }

  protected setAddEmail(v: string): void {
    this.addEmail.set(v);
  }
  protected setAddRole(v: OrgRole): void {
    this.addRole.set(v);
  }

  protected addMember(): void {
    const email = this.addEmail().trim();
    if (!email) return;
    this.error.set(null);
    this.api.addMemberByEmail(this.orgId(), email, this.addRole()).subscribe({
      next: () => {
        this.addEmail.set('');
        this.loadMembers();
      },
      error: (res) =>
        this.error.set(res?.error?.error ?? 'Could not add the member. Check the email address.'),
    });
  }

  protected setRole(userId: string, role: OrgRole): void {
    this.error.set(null);
    this.api.setMemberRole(this.orgId(), userId, role).subscribe({
      next: () => this.loadMembers(),
      error: (res) => {
        this.error.set(res?.error?.error ?? 'Could not change the role.');
        this.loadMembers(); // revert the dropdown to the server state
      },
    });
  }

  protected removeMember(userId: string): void {
    this.error.set(null);
    this.api.removeMember(this.orgId(), userId).subscribe({
      next: () => this.loadMembers(),
      error: (res) => this.error.set(res?.error?.error ?? 'Could not remove the member.'),
    });
  }
}
