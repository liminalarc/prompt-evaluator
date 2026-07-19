import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { ChipList } from './chip-list';

@Component({
  imports: [ChipList],
  template: `<app-chip-list [labels]="labels" />`,
})
class Host {
  labels: string[] = [];
}

describe('ChipList', () => {
  function render(labels: string[]) {
    TestBed.configureTestingModule({ imports: [Host] });
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.labels = labels;
    fixture.detectChanges();
    return fixture;
  }

  it('renders one chip per label (separated, not concatenated) [2.19 W35]', () => {
    const fixture = render(['LlmJudge', 'Regex']);
    const chips = fixture.nativeElement.querySelectorAll('[data-testid="chip"]');
    expect(chips.length).toBe(2);
    expect(chips[0].textContent).toContain('LlmJudge');
    expect(chips[1].textContent).toContain('Regex');
    // The host lays chips out with a gap so they never read as one word.
    expect(
      getComputedStyle(fixture.nativeElement.querySelector('app-chip-list')).display,
    ).toContain('flex');
  });

  it('renders an em-dash when the list is empty', () => {
    const fixture = render([]);
    expect(fixture.nativeElement.querySelector('[data-testid="chip"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="chip-list-empty"]')).not.toBeNull();
  });
});
