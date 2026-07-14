import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="auth" data-testid="forgot-page">
      <div class="auth__head">
        <h1 class="auth__title">Forgot password</h1>
        <p class="auth__subtitle">Enter your email and we'll send a reset link.</p>
      </div>

      @if (sent()) {
        <p class="auth__note" data-testid="forgot-sent">
          If an account exists for that email, a password reset link is on its way.
        </p>
      } @else {
        <form class="auth__form" (submit)="submit($event)">
          <div class="sb-field">
            <label for="email">Email</label>
            <input
              id="email"
              name="email"
              type="email"
              autocomplete="email"
              [ngModel]="email()"
              (ngModelChange)="email.set($event)"
            />
          </div>
          <button
            class="sb-btn sb-btn--primary"
            type="submit"
            data-testid="forgot-submit"
            [disabled]="submitting()"
          >
            Send reset link
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
export class ForgotPassword {
  private readonly auth = inject(AuthService);

  protected readonly email = signal('');
  protected readonly submitting = signal(false);
  protected readonly sent = signal(false);

  protected submit(event: Event): void {
    event.preventDefault();
    const email = this.email().trim();
    if (!email || this.submitting()) return;

    this.submitting.set(true);
    // The API is enumeration-resistant (always 200); show the same confirmation regardless.
    this.auth.forgotPassword(email).subscribe({
      next: () => {
        this.submitting.set(false);
        this.sent.set(true);
      },
      error: () => {
        this.submitting.set(false);
        this.sent.set(true);
      },
    });
  }
}
