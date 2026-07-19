import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Card, EmptyState, ErrorState, LoadingState, PageHeader } from '../shared';
import { OrgContextStore } from '../shared/org-context.store';
import { OrganizationsApiService } from '../organizations/organizations-api.service';
import { OrgDirectoryEntry } from '../organizations/org-admin.model';

/**
 * The first-run surface for a user in **zero** organizations (2.21). Before this, org-scoped pages
 * (Dashboard, Prompts) simply rendered nothing when there was no current org. Now they render this:
 * two real paths — **create an organization**, or **request to join** an existing one from the
 * directory (an owner then approves). Creating an org makes it current, which flips the parent's
 * null-org branch and unmounts this. Requesting leaves the user org-less until an owner approves, so
 * the request shows as "Requested" and the onboarding stays until they're a member.
 */
@Component({
  selector: 'app-no-org-onboarding',
  imports: [FormsModule, Card, EmptyState, ErrorState, LoadingState, PageHeader],
  template: `
    <section class="panel" data-testid="no-org-onboarding">
      <app-page-header
        heading="Welcome to LitmusAI"
        subtitle="You’re not in an organization yet. Create one, or request to join an existing team."
      />

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      <div class="onboarding-grid">
        <app-card heading="Create an organization">
          <p class="muted">Start fresh — you’ll be its owner and can invite teammates.</p>
          <form class="form-stack" (submit)="createOrg($event)" (keydown.escape)="orgName.set('')">
            <div class="sb-field">
              <label for="onboardOrgName">Organization name</label>
              <input
                id="onboardOrgName"
                name="onboardOrgName"
                data-testid="onboarding-org-name"
                [ngModel]="orgName()"
                (ngModelChange)="orgName.set($event)"
              />
            </div>
            <div class="form-actions">
              <button
                class="sb-btn sb-btn--primary"
                type="submit"
                data-testid="onboarding-create-org"
                [disabled]="!orgName().trim() || creating()"
              >
                {{ creating() ? 'Creating…' : 'Create organization' }}
              </button>
            </div>
          </form>
        </app-card>

        <app-card heading="Request to join an organization">
          <p class="muted">Ask an existing team’s owner for access — they’ll approve or decline.</p>

          @if (loadingDir()) {
            <app-loading-state label="Loading organizations…" />
          } @else if (directory().length === 0) {
            <app-empty-state message="No organizations exist yet — create the first one." />
          } @else {
            <ul class="dir-list" data-testid="onboarding-directory">
              @for (o of directory(); track o.id) {
                <li class="dir-row" [attr.data-org-id]="o.id">
                  <span class="dir-name">{{ o.name }}</span>
                  @if (o.isMember) {
                    <span class="muted" data-testid="dir-member">Member</span>
                  } @else if (o.hasPendingRequest) {
                    <span class="muted" data-testid="dir-requested">Requested</span>
                  } @else {
                    <button
                      type="button"
                      class="sb-btn sb-btn--secondary sb-btn--sm"
                      data-testid="request-access"
                      [disabled]="requestingId() === o.id"
                      (click)="requestAccess(o.id)"
                    >
                      {{ requestingId() === o.id ? 'Requesting…' : 'Request access' }}
                    </button>
                  }
                </li>
              }
            </ul>
          }
        </app-card>
      </div>
    </section>
  `,
  styles: [
    `
      .onboarding-grid {
        display: grid;
        gap: var(--sb-space-lg);
        grid-template-columns: repeat(auto-fit, minmax(18rem, 1fr));
        align-items: start;
      }
      .muted {
        color: var(--sb-text-secondary);
        font-size: var(--sb-type-small-size);
        margin: 0 0 var(--sb-space-md);
      }
      .dir-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: var(--sb-space-xs);
      }
      .dir-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--sb-space-md);
        padding: var(--sb-space-sm) var(--sb-space-md);
        border: 1px solid var(--sb-border);
        border-radius: var(--sb-radius-md);
      }
      .dir-name {
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        font-weight: var(--sb-type-h3-weight);
      }
    `,
  ],
})
export class NoOrgOnboarding {
  private readonly orgStore = inject(OrgContextStore);
  private readonly api = inject(OrganizationsApiService);

  protected readonly orgName = signal('');
  protected readonly creating = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly directory = signal<OrgDirectoryEntry[]>([]);
  protected readonly loadingDir = signal(true);
  protected readonly requestingId = signal<string | null>(null);

  constructor() {
    this.loadDirectory();
  }

  private loadDirectory(): void {
    this.loadingDir.set(true);
    this.api.listDirectory().subscribe({
      next: (entries) => {
        this.directory.set(entries);
        this.loadingDir.set(false);
      },
      error: () => {
        this.loadingDir.set(false);
        this.error.set('Could not load organizations.');
      },
    });
  }

  protected createOrg(event: Event): void {
    event.preventDefault();
    const name = this.orgName().trim();
    if (!name || this.creating()) return;
    this.creating.set(true);
    this.error.set(null);
    this.api.createOrganization(name).subscribe({
      next: (created) => {
        this.creating.set(false);
        this.orgName.set('');
        this.orgStore.add(created); // makes it current → the parent unmounts this onboarding
      },
      error: () => {
        this.creating.set(false);
        this.error.set('Could not create the organization.');
      },
    });
  }

  protected requestAccess(orgId: string): void {
    if (this.requestingId()) return;
    this.requestingId.set(orgId);
    this.error.set(null);
    this.api.requestAccess(orgId).subscribe({
      next: () => {
        this.requestingId.set(null);
        // Reflect the pending state locally without a refetch.
        this.directory.update((list) =>
          list.map((o) => (o.id === orgId ? { ...o, hasPendingRequest: true } : o)),
        );
      },
      error: (res) => {
        this.requestingId.set(null);
        this.error.set(res?.error?.error ?? 'Could not send the request.');
      },
    });
  }
}
