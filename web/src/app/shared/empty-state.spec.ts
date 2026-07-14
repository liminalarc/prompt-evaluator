import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EmptyState } from './empty-state';

@Component({
  imports: [EmptyState],
  template: `<app-empty-state [message]="msg"
    ><button data-testid="projected-action">Add</button></app-empty-state
  >`,
})
class Host {
  msg = 'No datasets yet.';
}

describe('EmptyState', () => {
  it('renders the message', () => {
    const fixture = TestBed.createComponent(EmptyState);
    fixture.componentRef.setInput('message', 'No prompts in this folder.');
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="empty"]') as HTMLElement;
    expect(el).toBeTruthy();
    expect(el.textContent).toContain('No prompts in this folder.');
  });

  it('projects an optional action', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="projected-action"]')).toBeTruthy();
  });
});
