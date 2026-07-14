import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PageHeader } from './page-header';

@Component({
  imports: [PageHeader],
  template: `<app-page-header [heading]="'Prompts'" [subtitle]="'Browse the registry'">
    <button actions data-testid="projected-action">New</button>
  </app-page-header>`,
})
class Host {}

describe('PageHeader', () => {
  it('renders the heading and subtitle', () => {
    const fixture = TestBed.createComponent(PageHeader);
    fixture.componentRef.setInput('heading', 'Datasets');
    fixture.componentRef.setInput('subtitle', 'Every dataset lives with a prompt');
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="page-header"]')).toBeTruthy();
    expect(el.querySelector('.page-header__title')?.textContent).toContain('Datasets');
    expect(el.querySelector('.page-header__subtitle')?.textContent).toContain(
      'Every dataset lives with a prompt',
    );
  });

  it('omits the subtitle element when none is given', () => {
    const fixture = TestBed.createComponent(PageHeader);
    fixture.componentRef.setInput('heading', 'Runs');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.page-header__subtitle')).toBeNull();
  });

  it('projects a primary action into the actions slot', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="projected-action"]')).toBeTruthy();
  });
});
