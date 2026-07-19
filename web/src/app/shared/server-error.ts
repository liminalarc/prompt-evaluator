import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extracts the API's structured `{ error }` message from a failed request (B2). Returns null when
 * the body isn't the structured shape (e.g. a timeout/gateway 5xx returns HTML or an empty body),
 * so callers can fall back to a loud generic message via `runFailureMessage`.
 */
export function serverError(err: unknown): string | null {
  const body = (err as HttpErrorResponse)?.error;
  return body && typeof body === 'object' && typeof body.error === 'string' ? body.error : null;
}

/**
 * A loud, never-silent message for a failed eval run (R2). B2 made the *structured* eval-runner
 * error loud, but a timeout or an infra/gateway 5xx returns a non-JSON body — so `serverError`
 * can't extract a message and the run would fail with nothing on screen. This always yields a clear
 * banner: the structured error when present, else a timeout- or server-error-flavored message.
 */
export function runFailureMessage(err: unknown): string {
  const structured = serverError(err);
  if (structured) return structured;

  const status = (err as HttpErrorResponse)?.status ?? 0;
  // status 0 = network-level abort/timeout (no response); 408/504 = request/gateway timeout.
  if (status === 0 || status === 408 || status === 504) {
    return (
      'The run did not finish in time — it likely timed out. Heavy prompts or larger datasets can ' +
      'exceed the current synchronous run limit; try again, or reduce the dataset size.'
    );
  }
  if (status >= 500) {
    return (
      `The run failed with a server error (HTTP ${status}) and no details — the eval-runner or ` +
      'gateway may be down, or the request may have timed out.'
    );
  }
  return 'Could not run the evaluation.';
}
