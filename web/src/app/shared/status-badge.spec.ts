import { TestBed } from '@angular/core/testing';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [StatusBadge] }));

  it('renders the label inside a brand badge with the variant modifier', () => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('variant', 'success');
    fixture.componentRef.setInput('label', 'Pass');
    fixture.detectChanges();

    const el = fixture.nativeElement.querySelector('[data-testid="status-badge"]') as HTMLElement;
    expect(el).toBeTruthy();
    expect(el.classList).toContain('sb-badge');
    expect(el.classList).toContain('sb-badge--success');
    expect(el.textContent?.trim()).toBe('Pass');
  });

  it('defaults to the neutral variant', () => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('label', 'x');
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="status-badge"]') as HTMLElement;
    expect(el.classList).toContain('sb-badge--neutral');
  });
});
