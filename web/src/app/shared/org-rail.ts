import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OrgContextStore } from './org-context.store';
import { AuthService } from '../auth/auth.service';

/**
 * The persistent left **organization rail** (2.19 W39 / 2.20). A flat list of every org the user can
 * access, with exactly one active at a time — the single-selection {@link OrgContextStore} model is
 * unchanged; this just replaces the cramped topbar `<select>` with a surface where you can *see* all
 * your orgs at a glance and switch. Selecting rescopes the app in place (pages react to
 * `currentOrgId`). The current org's Owner (or a workspace admin) also gets a Manage-members link.
 */
@Component({
  selector: 'app-org-rail',
  imports: [RouterLink],
  template: `
    <nav class="org-rail" aria-label="Organizations" data-testid="org-rail">
      <span class="org-rail__title">Organizations</span>

      @if (orgs().length === 0) {
        <p class="org-rail__empty">No organizations yet.</p>
      } @else {
        <ul class="org-rail__list">
          @for (o of orgs(); track o.id) {
            <li>
              <button
                type="button"
                class="org-rail__item"
                [class.org-rail__item--active]="o.id === currentId()"
                [attr.aria-current]="o.id === currentId() ? 'true' : null"
                [attr.data-testid]="'org-option'"
                [attr.data-org-id]="o.id"
                (click)="select(o.id)"
              >
                {{ o.name }}
              </button>
            </li>
          }
        </ul>
      }

      @if (canManageCurrent() && currentId(); as orgId) {
        <a
          class="org-rail__manage"
          data-testid="manage-org"
          [routerLink]="['/organizations', orgId]"
          title="Manage members"
          >Manage members</a
        >
      }
    </nav>
  `,
  styles: [
    `
      .org-rail {
        display: flex;
        flex-direction: column;
        gap: var(--sb-space-xs);
        padding: var(--sb-space-md);
      }
      .org-rail__title {
        text-transform: uppercase;
        letter-spacing: 0.08em;
        font-size: var(--sb-type-caption-size);
        color: var(--sb-text-muted);
        margin-bottom: var(--sb-space-xs);
      }
      .org-rail__empty {
        margin: 0;
        color: var(--sb-text-muted);
        font-size: var(--sb-type-small-size);
      }
      .org-rail__list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 2px;
      }
      .org-rail__item {
        display: block;
        width: 100%;
        text-align: left;
        padding: var(--sb-space-sm) var(--sb-space-md);
        border: 0;
        border-radius: var(--sb-radius-md);
        background: transparent;
        color: var(--sb-text-secondary);
        font-size: var(--sb-type-small-size);
        cursor: pointer;
        transition:
          background 0.15s ease,
          color 0.15s ease;
      }
      .org-rail__item:hover {
        background: var(--sb-surface-variant);
        color: var(--sb-text);
      }
      .org-rail__item--active {
        background: var(--sb-surface-variant);
        color: var(--sb-text);
        font-weight: var(--sb-type-h3-weight);
        box-shadow: inset 3px 0 0 var(--sb-primary);
      }
      .org-rail__manage {
        margin-top: var(--sb-space-sm);
        padding: var(--sb-space-xs) var(--sb-space-md);
        font-size: var(--sb-type-small-size);
        color: var(--sb-primary);
        text-decoration: none;
      }
      .org-rail__manage:hover {
        text-decoration: underline;
      }
    `,
  ],
})
export class OrgRail {
  private readonly org = inject(OrgContextStore);
  private readonly auth = inject(AuthService);

  protected readonly orgs = this.org.organizations;
  protected readonly currentId = this.org.currentOrgId;

  /** The Manage link shows for the current org's Owner (or a workspace admin) — 4.5 gate. */
  protected readonly canManageCurrent = computed(
    () => this.org.currentOrg()?.role === 'Owner' || !!this.auth.currentUser()?.isAdmin,
  );

  protected select(id: string): void {
    this.org.select(id);
  }
}
