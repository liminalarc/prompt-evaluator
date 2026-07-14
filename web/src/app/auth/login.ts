import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ErrorState } from '../shared';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink, ErrorState],
  template: `
    <section class="auth" data-testid="login-page">
      <div class="auth__head">
        <h1 class="auth__title">Sign in</h1>
        <p class="auth__subtitle">Sign in to LitmusAI.</p>
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
          <label for="password">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            autocomplete="current-password"
            [ngModel]="password()"
            (ngModelChange)="password.set($event)"
          />
        </div>
        <button
          class="sb-btn sb-btn--primary"
          type="submit"
          data-testid="login-submit"
          [disabled]="submitting()"
        >
          Sign in
        </button>
      </form>

      <div class="auth__links">
        <a routerLink="/register" data-testid="to-register">Create an account</a>
        <a routerLink="/forgot-password" data-testid="to-forgot">Forgot password?</a>
      </div>
    </section>
  `,
  styleUrl: './auth.css',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);

  protected submit(event: Event): void {
    event.preventDefault();
    const email = this.email().trim();
    const password = this.password();
    if (!email || !password || this.submitting()) return;

    this.submitting.set(true);
    this.error.set(null);
    this.auth.login(email, password).subscribe({
      next: () => {
        this.submitting.set(false);
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
        void this.router.navigateByUrl(returnUrl);
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('Incorrect email or password.');
      },
    });
  }
}
