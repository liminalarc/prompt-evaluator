import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { Breadcrumb } from './breadcrumb';

describe('Breadcrumb', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({ imports: [Breadcrumb], providers: [provideRouter([])] }),
  );

  it('renders a crumb per item, linking all but the last', () => {
    const fixture = TestBed.createComponent(Breadcrumb);
    fixture.componentRef.setInput('items', [
      { label: 'Prompts', link: '/prompts' },
      { label: 'Greeter' },
    ]);
    fixture.detectChanges();
    const crumbs = fixture.nativeElement.querySelectorAll('[data-testid="crumb"]');
    expect(crumbs.length).toBe(2);
    expect(crumbs[0].tagName).toBe('A');
    expect(crumbs[1].tagName).toBe('SPAN');
    expect(crumbs[1].getAttribute('aria-current')).toBe('page');
  });

  it('renders nothing when there are no items', () => {
    const fixture = TestBed.createComponent(Breadcrumb);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('[data-testid="crumb"]').length).toBe(0);
  });
});
