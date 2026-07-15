import { Component, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { OrgContextStore } from './shared/org-context.store';
import { ThemeService } from './shared';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly org = inject(OrgContextStore);
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  private readonly router = inject(Router);

  constructor() {
    // The org context (and its access-filtered switcher) only makes sense once signed in — load
    // it when authentication resolves, and drop it on sign-out so a new session starts clean.
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.org.load();
      } else {
        this.org.reset();
      }
    });
  }

  protected logout(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: () => {
        // Even if the network call fails, drop the local session and send them to login.
        this.auth.clearSession();
        void this.router.navigateByUrl('/login');
      },
    });
  }
}
