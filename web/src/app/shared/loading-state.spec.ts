import { TestBed } from '@angular/core/testing';
import { LoadingState } from './loading-state';

describe('LoadingState', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [LoadingState] }));

  it('renders a polite status region with the default label', () => {
    const fixture = TestBed.createComponent(LoadingState);
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="loading"]') as HTMLElement;
    expect(el).toBeTruthy();
    expect(el.getAttribute('role')).toBe('status');
    expect(el.textContent).toContain('Loading');
  });

  it('renders a custom label', () => {
    const fixture = TestBed.createComponent(LoadingState);
    fixture.componentRef.setInput('label', 'Fetching runs…');
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="loading"]') as HTMLElement;
    expect(el.textContent).toContain('Fetching runs…');
  });
});
