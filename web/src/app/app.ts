import { Component, ElementRef, HostListener, effect, inject, viewChild } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { OrgContextStore } from './shared/org-context.store';
import { ConfirmDialog, OrgRail, ThemeService, VersionService } from './shared';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ConfirmDialog, OrgRail],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly org = inject(OrgContextStore);
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  protected readonly version = inject(VersionService);
  private readonly router = inject(Router);

  /** The Admin disclosure (a native <details>); referenced so we can auto-close it. */
  private readonly adminMenu = viewChild<ElementRef<HTMLDetailsElement>>('adminMenu');

  // A native <details> only toggles via its own summary, so it "sticks" open on an outside click or
  // after picking an item. Close it on any click outside the disclosure, and on Escape.
  @HostListener('document:click', ['$event'])
  protected closeAdminOnOutsideClick(event: MouseEvent): void {
    const el = this.adminMenu()?.nativeElement;
    if (el?.open && !el.contains(event.target as Node)) {
      el.open = false;
    }
  }

  @HostListener('document:keydown.escape')
  protected closeAdminOnEscape(): void {
    const el = this.adminMenu()?.nativeElement;
    if (el?.open) {
      el.open = false;
    }
  }

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
