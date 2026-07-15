import { Injectable, Signal, signal } from '@angular/core';

/** What a confirm prompt shows (1.10). The message states what a destructive action cascades. */
export interface ConfirmRequest {
  title: string;
  message: string;
  /** Label for the confirming (destructive) action; defaults to "Delete". */
  confirmLabel?: string;
}

/**
 * A tiny app-level confirmation gate (1.10). Destructive actions call {@link ask} and await the
 * boolean; the single {@link ConfirmDialog} mounted in the app shell renders the pending request
 * and resolves it. Keeps confirmation consistent (and testable) instead of `window.confirm`.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly _request = signal<ConfirmRequest | null>(null);
  /** The pending request the dialog renders, or null when nothing is being confirmed. */
  readonly request: Signal<ConfirmRequest | null> = this._request.asReadonly();

  private resolver: ((confirmed: boolean) => void) | null = null;

  /** Show a confirm prompt; resolves true if confirmed, false if cancelled/dismissed. */
  ask(request: ConfirmRequest): Promise<boolean> {
    // A second ask while one is open cancels the first — one dialog at a time.
    this.resolver?.(false);
    this._request.set(request);
    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
    });
  }

  /** Confirm the pending request (the dialog's destructive button). */
  confirm(): void {
    this.settle(true);
  }

  /** Dismiss the pending request (cancel button / backdrop / Esc). */
  cancel(): void {
    this.settle(false);
  }

  private settle(confirmed: boolean): void {
    this._request.set(null);
    const resolve = this.resolver;
    this.resolver = null;
    resolve?.(confirmed);
  }
}
