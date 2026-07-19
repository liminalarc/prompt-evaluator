import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OrgContextStore } from './org-context.store';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { AuthService } from '../auth/auth.service';

/**
 * The persistent left **organization rail** (2.19 W39 / 2.20). A flat list of every org the user can
 * access, with exactly one active at a time — the single-selection {@link OrgContextStore} model is
 * unchanged; this just replaces the cramped topbar `<select>` with a surface where you can *see* all
 * your orgs at a glance and switch. Selecting rescopes the app in place. Supports a **collapsed**
 * state (initials only) to reclaim horizontal real estate, and creating a new org inline. The current
 * org's Owner (or a workspace admin) also gets a Manage-members link.
 */
@Component({
  selector: 'app-org-rail',
  imports: [FormsModule, RouterLink],
  template: `
    <nav
      class="org-rail"
      [class.org-rail--collapsed]="collapsed()"
      aria-label="Organizations"
      data-testid="org-rail"
    >
      <div class="org-rail__head">
        @if (!collapsed()) {
          <span class="org-rail__title">Organizations</span>
        }
        <button
          type="button"
          class="org-rail__collapse"
          [attr.aria-label]="collapsed() ? 'Expand organizations' : 'Collapse organizations'"
          [attr.aria-expanded]="!collapsed()"
          [attr.title]="collapsed() ? 'Expand' : 'Collapse'"
          data-testid="rail-collapse"
          (click)="toggleCollapsed.emit()"
        >
          {{ collapsed() ? '»' : '«' }}
        </button>
      </div>

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
              [attr.title]="o.name"
              (click)="select(o.id)"
            >
              @if (collapsed()) {
                <span class="org-rail__initial" aria-hidden="true">{{ initial(o.name) }}</span>
                <span class="sr-only">{{ o.name }}</span>
              } @else {
                {{ o.name }}
              }
            </button>
          </li>
        } @empty {
          @if (!collapsed()) {
            <p class="org-rail__empty">No organizations yet.</p>
          }
        }
      </ul>

      @if (!collapsed()) {
        @if (creatingOrg()) {
          <form
            class="org-rail__new"
            (submit)="createOrg($event)"
            (keydown.escape)="cancelCreateOrg()"
          >
            <input
              class="sb-field"
              placeholder="Organization name"
              aria-label="New organization name"
              name="newOrgName"
              data-testid="rail-new-org-name"
              [ngModel]="newOrgName()"
              (ngModelChange)="newOrgName.set($event)"
            />
            <div class="org-rail__new-actions">
              <button
                type="submit"
                class="sb-btn sb-btn--primary sb-btn--sm"
                data-testid="rail-create-org"
                [disabled]="!newOrgName().trim() || saving()"
              >
                {{ saving() ? 'Adding…' : 'Add' }}
              </button>
              <button
                type="button"
                class="sb-btn sb-btn--ghost sb-btn--sm"
                (click)="cancelCreateOrg()"
              >
                Cancel
              </button>
            </div>
            @if (createError()) {
              <p class="org-rail__error" data-testid="rail-org-error">{{ createError() }}</p>
            }
          </form>
        } @else {
          <button
            type="button"
            class="org-rail__add"
            data-testid="rail-add-org"
            (click)="startCreateOrg()"
          >
            + New organization
          </button>
        }
      }

      @if (!collapsed() && canManageCurrent() && currentId(); as orgId) {
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
      .org-rail--collapsed {
        padding: var(--sb-space-sm) var(--sb-space-xs);
        align-items: center;
      }
      .org-rail__head {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: var(--sb-space-xs);
        width: 100%;
      }
      .org-rail--collapsed .org-rail__head {
        justify-content: center;
      }
      .org-rail__title {
        text-transform: uppercase;
        letter-spacing: 0.08em;
        font-size: var(--sb-type-caption-size);
        color: var(--sb-text-muted);
      }
      .org-rail__collapse {
        border: 0;
        background: transparent;
        color: var(--sb-text-muted);
        cursor: pointer;
        font-size: 1rem;
        line-height: 1;
        padding: var(--sb-space-xs);
        border-radius: var(--sb-radius-sm);
      }
      .org-rail__collapse:hover {
        background: var(--sb-surface-variant);
        color: var(--sb-text);
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
        width: 100%;
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
      /* Collapsed: square, centered initial tiles; the active bar wraps the tile. */
      .org-rail--collapsed .org-rail__item {
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 0;
        width: 2.25rem;
        height: 2.25rem;
        margin: 0 auto;
      }
      .org-rail__initial {
        font-weight: var(--sb-type-h3-weight);
        text-transform: uppercase;
      }
      .org-rail__add {
        margin-top: var(--sb-space-xs);
        padding: var(--sb-space-sm) var(--sb-space-md);
        border: 0;
        border-radius: var(--sb-radius-md);
        background: transparent;
        color: var(--sb-text-secondary);
        font-size: var(--sb-type-small-size);
        text-align: left;
        cursor: pointer;
      }
      .org-rail__add:hover {
        background: var(--sb-surface-variant);
        color: var(--sb-text);
      }
      .org-rail__new {
        display: flex;
        flex-direction: column;
        gap: var(--sb-space-xs);
        margin-top: var(--sb-space-xs);
      }
      .org-rail__new-actions {
        display: flex;
        gap: var(--sb-space-xs);
      }
      .org-rail__error {
        margin: 0;
        color: var(--sb-error);
        font-size: var(--sb-type-caption-size);
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
      .sr-only {
        position: absolute;
        width: 1px;
        height: 1px;
        padding: 0;
        margin: -1px;
        overflow: hidden;
        clip: rect(0, 0, 0, 0);
        white-space: nowrap;
        border: 0;
      }
    `,
  ],
})
export class OrgRail {
  private readonly org = inject(OrgContextStore);
  private readonly orgsApi = inject(OrganizationsApiService);
  private readonly auth = inject(AuthService);

  /** Collapsed (initials-only) state — owned by the shell so it can narrow the grid column. */
  readonly collapsed = input<boolean>(false);
  readonly toggleCollapsed = output<void>();

  protected readonly orgs = this.org.organizations;
  protected readonly currentId = this.org.currentOrgId;

  protected readonly creatingOrg = signal(false);
  protected readonly newOrgName = signal('');
  protected readonly saving = signal(false);
  protected readonly createError = signal<string | null>(null);

  /** The Manage link shows for the current org's Owner (or a workspace admin) — 4.5 gate. */
  protected readonly canManageCurrent = computed(
    () => this.org.currentOrg()?.role === 'Owner' || !!this.auth.currentUser()?.isAdmin,
  );

  protected initial(name: string): string {
    return name.trim().charAt(0) || '?';
  }

  protected select(id: string): void {
    this.org.select(id);
  }

  protected startCreateOrg(): void {
    this.creatingOrg.set(true);
    this.newOrgName.set('');
    this.createError.set(null);
  }

  protected cancelCreateOrg(): void {
    this.creatingOrg.set(false);
    this.newOrgName.set('');
    this.createError.set(null);
  }

  protected createOrg(event: Event): void {
    event.preventDefault();
    const name = this.newOrgName().trim();
    if (!name || this.saving()) return;
    this.saving.set(true);
    this.createError.set(null);
    this.orgsApi.createOrganization(name).subscribe({
      next: (created) => {
        this.saving.set(false);
        this.creatingOrg.set(false);
        this.newOrgName.set('');
        this.org.add(created); // append + make current
      },
      error: () => {
        this.saving.set(false);
        this.createError.set('Could not create the organization.');
      },
    });
  }
}
