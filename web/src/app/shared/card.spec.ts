import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Card, CardFoot } from './card';

@Component({
  imports: [Card, CardFoot],
  template: `
    <app-card [heading]="heading">
      <p class="body-content">Body</p>
      @if (withFoot) {
        <button foot type="button">Save</button>
      }
    </app-card>
  `,
})
class Host {
  heading = '';
  withFoot = false;
}

describe('Card', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [Host] }));

  function render(setup: (h: Host) => void) {
    const fixture = TestBed.createComponent(Host);
    setup(fixture.componentInstance);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the brand card with projected body content', () => {
    const el = render(() => {});
    const card = el.querySelector('[data-testid="card"]') as HTMLElement;
    expect(card.classList).toContain('sb-card');
    const body = card.querySelector('.sb-card__body') as HTMLElement;
    expect(body.querySelector('.body-content')?.textContent).toBe('Body');
  });

  it('renders a header only when a heading is supplied', () => {
    expect(render(() => {}).querySelector('.sb-card__head')).toBeNull();

    const withHeading = render((h) => (h.heading = 'Version history'));
    const head = withHeading.querySelector('.sb-card__head') as HTMLElement;
    expect(head.querySelector('.sb-card__title')?.textContent?.trim()).toBe('Version history');
  });

  it('renders the foot region only when foot content is projected', () => {
    expect(render(() => {}).querySelector('.sb-card__foot')).toBeNull();

    const withFoot = render((h) => (h.withFoot = true));
    const foot = withFoot.querySelector('.sb-card__foot') as HTMLElement;
    expect(foot).not.toBeNull();
    expect(foot.querySelector('[foot]')?.textContent?.trim()).toBe('Save');
  });
});
