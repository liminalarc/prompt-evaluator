import { HttpErrorResponse } from '@angular/common/http';
import { runFailureMessage, serverError } from './server-error';

describe('server-error helpers', () => {
  it('extracts the structured {error} message (B2)', () => {
    const err = new HttpErrorResponse({
      status: 502,
      error: { error: 'eval-runner: Anthropic not configured.' },
    });
    expect(serverError(err)).toBe('eval-runner: Anthropic not configured.');
    // runFailureMessage prefers the structured message when present.
    expect(runFailureMessage(err)).toBe('eval-runner: Anthropic not configured.');
  });

  it('returns null for a non-structured body', () => {
    const htmlBody = new HttpErrorResponse({ status: 502, error: '<html>502 Bad Gateway</html>' });
    expect(serverError(htmlBody)).toBeNull();
  });

  it('gives a loud timeout message for a status-0 / 504 failure (R2)', () => {
    for (const status of [0, 408, 504]) {
      const err = new HttpErrorResponse({ status, error: null });
      const msg = runFailureMessage(err);
      expect(msg).toContain('timed out');
      expect(msg.length).toBeGreaterThan(0);
    }
  });

  it('gives a loud generic 5xx message for a non-JSON gateway error (R2)', () => {
    const err = new HttpErrorResponse({ status: 502, error: '<html>bad gateway</html>' });
    const msg = runFailureMessage(err);
    expect(msg).toContain('502');
    expect(msg).toContain('server error');
  });

  it('never returns an empty string — a run failure is always loud (R2)', () => {
    expect(runFailureMessage(new HttpErrorResponse({ status: 400 })).length).toBeGreaterThan(0);
    expect(runFailureMessage(undefined).length).toBeGreaterThan(0);
  });
});
