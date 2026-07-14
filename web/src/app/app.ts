import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { OrgContextStore } from './shared/org-context.store';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly org = inject(OrgContextStore);

  constructor() {
    // Resolve the global org context once, at the shell — every page reads from it thereafter.
    this.org.load();
  }
}
