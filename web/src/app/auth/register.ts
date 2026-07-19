import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ErrorState } from '../shared';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink, ErrorState],
  template: `
    <section class="auth" data-testid="register-page">
      <div class="auth__head">
        <h1 class="auth__title">Create account</h1>
        <p class="auth__subtitle">
          Register for LitmusAI. After signing up you can create an organization or request to join
          an existing one.
        </p>
      </div>

      @if (error(); as message) {
        <app-error-state [message]="message" />
      }

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
        <div class="sb-field">
          <label for="displayName">Display name</label>
          <input
            id="displayName"
            name="displayName"
            autocomplete="name"
            [ngModel]="displayName()"
            (ngModelChange)="displayName.set($event)"
          />
        </div>
        <div class="sb-field">
          <label for="password">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            autocomplete="new-password"
            [ngModel]="password()"
            (ngModelChange)="password.set($event)"
          />
        </div>
        <button
          class="sb-btn sb-btn--primary"
          type="submit"
          data-testid="register-submit"
          [disabled]="submitting()"
        >
          Create account
        </button>
      </form>

      <div class="auth__links">
        <a routerLink="/login" data-testid="to-login">Already have an account? Sign in</a>
      </div>
    </section>
  `,
  styleUrl: './auth.css',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly displayName = signal('');
  protected readonly password = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);

  protected submit(event: Event): void {
    event.preventDefault();
    const email = this.email().trim();
    const displayName = this.displayName().trim();
    const password = this.password();
    if (!email || !displayName || !password || this.submitting()) return;

    this.submitting.set(true);
    this.error.set(null);
    this.auth.register(email, displayName, password).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl('/');
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('Could not create the account — the email may already be registered.');
      },
    });
  }
}
