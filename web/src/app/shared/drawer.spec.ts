import { TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { Drawer } from './drawer';

@Component({
  imports: [Drawer],
  template: `
    <app-drawer [open]="open()" heading="Edit thing" (closed)="open.set(false)">
      <p>Body content</p>
      <button data-testid="inner">Save</button>
    </app-drawer>
  `,
})
class Host {
  open = signal(false);
}

describe('Drawer [2.19 D1]', () => {
  it('renders nothing until open, then shows heading + projected content', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="drawer"]')).toBeFalsy();

    fixture.componentInstance.open.set(true);
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="drawer"]')).toBeTruthy();
    expect(el.querySelector('.drawer__title')!.textContent).toContain('Edit thing');
    expect(el.querySelector('[data-testid="inner"]')).toBeTruthy();
  });

  it('closes on the close button and on scrim click', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.open.set(true);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    (el.querySelector('[data-testid="drawer-close"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.componentInstance.open()).toBeFalse();
    expect(el.querySelector('[data-testid="drawer"]')).toBeFalsy();

    // Re-open, then click the scrim (outside the panel) to close.
    fixture.componentInstance.open.set(true);
    fixture.detectChanges();
    (el.querySelector('[data-testid="drawer-scrim"]') as HTMLElement).click();
    fixture.detectChanges();
    expect(fixture.componentInstance.open()).toBeFalse();
  });

  it('does not close when the panel body itself is clicked', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.open.set(true);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    (el.querySelector('[data-testid="inner"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.componentInstance.open()).toBeTrue();
  });
});
