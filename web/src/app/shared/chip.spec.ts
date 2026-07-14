import { TestBed } from '@angular/core/testing';
import { Chip } from './chip';

describe('Chip', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [Chip] }));

  it('renders the label inside a brand chip', () => {
    const fixture = TestBed.createComponent(Chip);
    fixture.componentRef.setInput('label', 'LlmJudge');
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="chip"]') as HTMLElement;
    expect(el.classList).toContain('sb-chip');
    expect(el.textContent?.trim()).toBe('LlmJudge');
  });
});
