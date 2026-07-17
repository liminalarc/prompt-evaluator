import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Breadcrumb, Card, Crumb, ErrorState, PageHeader } from '../shared';
import { AuthService } from '../auth/auth.service';

/**
 * The signed-in user's own account page (spec 4.3): self-service change-password (current + new),
 * no email. Any authenticated user; reached from the top-bar user chip.
 */
@Component({
  selector: 'app-account',
  imports: [FormsModule, Breadcrumb, Card, ErrorState, PageHeader],
  template: `
    <section class="panel">
      <app-breadcrumb [items]="crumbs()" />
      <app-page-header heading="Account" [subtitle]="auth.currentUser()?.email ?? ''" />

      <app-card heading="Change password">
        @if (error(); as message) {
          <app-error-state [message]="message" />
        }
        @if (saved()) {
          <p class="saved" data-testid="password-saved">Password changed.</p>
        }
        <form class="form-stack" (submit)="submit($event)">
          <div class="sb-field">
            <label for="current">Current password</label>
            <input
              id="current"
              name="current"
              type="password"
              data-testid="current-password"
              [ngModel]="current()"
              (ngModelChange)="current.set($event)"
            />
          </div>
          <div class="sb-field">
            <label for="next">New password</label>
            <input
              id="next"
              name="next"
              type="password"
              data-testid="next-password"
              [ngModel]="next()"
              (ngModelChange)="next.set($event)"
            />
          </div>
          <button
            class="sb-btn sb-btn--primary"
            type="submit"
            data-testid="change-password"
            [disabled]="saving()"
          >
            {{ saving() ? 'Saving…' : 'Change password' }}
          </button>
        </form>
      </app-card>
    </section>
  `,
  styles: [
    `
      .saved {
        color: var(--sb-success, var(--sb-text-secondary));
        font-size: var(--sb-type-small-size);
      }
    `,
  ],
})
export class Account {
  protected readonly auth = inject(AuthService);

  protected readonly current = signal('');
  protected readonly next = signal('');
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly crumbs = computed<Crumb[]>(() => [
    { label: 'Dashboard', link: '/' },
    { label: 'Account' },
  ]);

  protected submit(event: Event): void {
    event.preventDefault();
    const current = this.current();
    const next = this.next();
    if (!current || !next) {
      this.error.set('Enter your current and new password.');
      return;
    }
    this.error.set(null);
    this.saved.set(false);
    this.saving.set(true);
    this.auth.changePassword(current, next).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.set(true);
        this.current.set('');
        this.next.set('');
      },
      error: () => {
        this.saving.set(false);
        this.error.set(
          'Could not change password — check your current password and the policy (min 8 chars).',
        );
      },
    });
  }
}
