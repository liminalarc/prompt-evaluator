import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from './api.service';
import { EchoResult } from './echo';

@Component({
  selector: 'app-home',
  imports: [FormsModule],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {
  private readonly api = inject(ApiService);

  protected readonly prompt = signal('Hello, LitmusAI');
  protected readonly result = signal<EchoResult | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(false);

  run(): void {
    const prompt = this.prompt().trim();
    if (!prompt || this.loading()) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);
    this.api.echo(prompt).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Round trip failed — is the stack running?');
        this.loading.set(false);
      },
    });
  }
}
