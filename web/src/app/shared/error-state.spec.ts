import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ErrorState } from './error-state';

@Component({
  imports: [ErrorState],
  template: `<app-error-state
    [message]="msg"
    [retryable]="retryable"
    (retry)="retried = retried + 1"
  />`,
})
class Host {
  msg = 'Could not load the dataset.';
  retryable = false;
  retried = 0;
}

describe('ErrorState', () => {
  it('renders the message in a brand field-error alert', () => {
    const fixture = TestBed.createComponent(ErrorState);
    fixture.componentRef.setInput('message', 'boom');
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="error"]') as HTMLElement;
    expect(el).toBeTruthy();
    expect(el.getAttribute('role')).toBe('alert');
    expect(el.classList).toContain('sb-field--error');
    expect(el.textContent).toContain('boom');
  });

  it('shows a retry button only when retryable and emits on click', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    let btn = fixture.nativeElement.querySelector('[data-testid="error-retry"]');
    expect(btn).toBeNull();

    fixture.componentInstance.retryable = true;
    fixture.detectChanges();
    btn = fixture.nativeElement.querySelector('[data-testid="error-retry"]') as HTMLButtonElement;
    expect(btn).toBeTruthy();

    btn.click();
    expect(fixture.componentInstance.retried).toBe(1);
  });
});
