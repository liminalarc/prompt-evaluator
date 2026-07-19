import { Component, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { OrgContextStore } from '../shared/org-context.store';
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
type OrgTab = 'overview' | 'members';

/**
 * The owner-facing organization detail page (spec 4.5, tabbed in 2.20 W40). Reached at
 * /organizations/:id via the org rail's **settings gear** (on the active org), shown to a member who
 * is the org's Owner (or a global admin). An Overview tab + a Members tab; the Members tab lists the
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
        subtitle="Your organization — its overview and members."
      />

      @if (loading()) {
        <app-loading-state label="Loading…" />
      } @else {
        <!-- W40: the org is a tabbed page (Overview · Members), mirroring the workspace hub. Panels
             stay in the DOM ([hidden]) so a tab switch is instant; tab syncs to ?tab=. -->
        <nav class="org-tabs" role="tablist">
          @for (t of tabs; track t.id) {
            <button
              type="button"
              role="tab"
              class="org-tabs__tab"
              [class.org-tabs__tab--active]="tab() === t.id"
              [attr.data-testid]="'org-tab-' + t.id"
              (click)="selectTab(t.id)"
            >
              {{ t.label }}
            </button>
          }
        </nav>

        <div [hidden]="tab() !== 'overview'">
          <app-card heading="Overview">
            <dl class="org-facts">
              <div class="org-facts__row">
                <dt>Your role</dt>
                <dd data-testid="my-role">{{ myRole() ?? 'Member' }}</dd>
              </div>
              @if (canManage()) {
                <div class="org-facts__row">
                  <dt>Members</dt>
                  <dd data-testid="member-count">{{ members().length }}</dd>
                </div>
              }
            </dl>
            @if (canManage()) {
              <p class="muted hint">
                Add, remove, and set roles in the
                <button type="button" class="linklike" (click)="selectTab('members')">
                  Members
                </button>
                tab.
              </p>
            } @else {
              <p class="muted" data-testid="no-permission">
                You don’t have permission to manage this organization’s members. Only an owner (or a
                workspace admin) can.
              </p>
            }
          </app-card>
        </div>

        <div [hidden]="tab() !== 'members'">
          @if (!canManage()) {
            <app-card heading="Members">
              <p class="muted">
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
        </div>
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
      /* Tab bar — same convention as the workspace hub (W39/W40). */
      .org-tabs {
        display: flex;
        gap: var(--sb-space-xs);
        border-bottom: 1px solid var(--sb-border);
        margin-bottom: var(--sb-space-lg);
      }
      .org-tabs__tab {
        border: none;
        background: transparent;
        padding: var(--sb-space-sm) var(--sb-space-md);
        cursor: pointer;
        color: var(--sb-text-muted);
        border-bottom: 2px solid transparent;
        font-size: var(--sb-type-body-size);
      }
      .org-tabs__tab--active {
        color: var(--sb-text);
        border-bottom-color: var(--sb-primary);
        font-weight: 600;
      }
      .org-facts {
        margin: 0;
        display: flex;
        flex-wrap: wrap;
        gap: var(--sb-space-xl);
      }
      .org-facts__row {
        display: flex;
        flex-direction: column;
        gap: 2px;
      }
      .org-facts dt {
        font-size: var(--sb-type-caption-size);
        letter-spacing: 0.05em;
        text-transform: uppercase;
        color: var(--sb-text-muted);
      }
      .org-facts dd {
        margin: 0;
        color: var(--sb-text);
        font-weight: var(--sb-type-h3-weight);
      }
      .linklike {
        border: 0;
        background: transparent;
        padding: 0;
        color: var(--sb-primary);
        cursor: pointer;
        font: inherit;
        text-decoration: underline;
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
  private readonly router = inject(Router);
  private readonly api = inject(OrganizationsApiService);
  private readonly auth = inject(AuthService);
  private readonly orgStore = inject(OrgContextStore);

  constructor() {
    // Rescope on an org switch from the rail: if the global org changes away from this page's org,
    // follow it — reload for the newly-selected org instead of getting stuck on the old one (the gear
    // opens the *active* org, so this keeps the page and the rail selection in lock-step). W40 follow-up.
    effect(() => {
      const ctxId = this.orgStore.currentOrgId();
      const pageId = untracked(() => this.orgId());
      if (ctxId && pageId && ctxId !== pageId) {
        this.orgId.set(ctxId);
        void this.router.navigate(['/organizations', ctxId], { replaceUrl: true });
        this.loadOrg(ctxId);
      }
    });
  }

  protected readonly roles = ROLES;
  protected readonly tabs: { id: OrgTab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'members', label: 'Members' },
  ];
  protected readonly tab = signal<OrgTab>('overview');
  private readonly tabIds: OrgTab[] = ['overview', 'members'];
  private readonly orgId = signal('');
  protected readonly orgName = signal('');
  protected readonly myRole = signal<OrgRole | undefined>(undefined);
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

  // Sync the active tab to ?tab= (explicit nav, replaceUrl) — the shared tabbed-surface convention,
  // so a deep-link / the rail's "Manage members" link can land straight on the Members tab.
  protected selectTab(t: OrgTab): void {
    this.tab.set(t);
    void this.router.navigate(['/organizations', this.orgId()], {
      queryParams: { tab: t },
      replaceUrl: true,
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.orgId.set(id);
    const urlTab = this.route.snapshot.queryParamMap.get('tab') as OrgTab | null;
    if (urlTab && this.tabIds.includes(urlTab)) this.tab.set(urlTab);
    this.loadOrg(id);
  }

  /** Resolve the org's name + the caller's role (from the member-scoped list), then its members. */
  private loadOrg(id: string): void {
    this.loading.set(true);
    this.error.set(null);
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
          this.members.set([]);
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
