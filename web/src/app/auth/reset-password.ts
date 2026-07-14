import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ErrorState } from '../shared';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-reset-password',
  imports: [FormsModule, RouterLink, ErrorState],
  template: `
    <section class="auth" data-testid="reset-page">
      <div class="auth__head">
        <h1 class="auth__title">Reset password</h1>
        <p class="auth__subtitle">Choose a new password for {{ email() || 'your account' }}.</p>
      </div>

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

      @if (done()) {
        <p class="auth__note" data-testid="reset-done">
          Your password has been reset. You can now sign in.
        </p>
      } @else {
        <form class="auth__form" (submit)="submit($event)">
          <div class="sb-field">
            <label for="newPassword">New password</label>
            <input
              id="newPassword"
              name="newPassword"
              type="password"
              autocomplete="new-password"
              [ngModel]="newPassword()"
              (ngModelChange)="newPassword.set($event)"
            />
          </div>
          <button
            class="sb-btn sb-btn--primary"
            type="submit"
            data-testid="reset-submit"
            [disabled]="submitting() || !token()"
          >
            Reset password
          </button>
        </form>
      }

      <div class="auth__links">
        <a routerLink="/login" data-testid="to-login">Back to sign in</a>
      </div>
    </section>
  `,
  styleUrl: './auth.css',
})
export class ResetPassword {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // The reset link carries the account + one-time token as query params.
  protected readonly email = signal(this.route.snapshot.queryParamMap.get('email') ?? '');
  protected readonly token = signal(this.route.snapshot.queryParamMap.get('token') ?? '');
  protected readonly newPassword = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly done = signal(false);

  protected submit(event: Event): void {
    event.preventDefault();
    const email = this.email().trim();
    const token = this.token();
    const newPassword = this.newPassword();
    if (!email || !token || !newPassword || this.submitting()) return;

    this.submitting.set(true);
    this.error.set(null);
    this.auth.resetPassword(email, token, newPassword).subscribe({
      next: () => {
        this.submitting.set(false);
        this.done.set(true);
        setTimeout(() => void this.router.navigateByUrl('/login'), 1500);
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('Could not reset the password — the link may have expired.');
      },
    });
  }
}
