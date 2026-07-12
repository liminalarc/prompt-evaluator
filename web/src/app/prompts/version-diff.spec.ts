import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { VersionDiff } from './version-diff';

@Component({
  imports: [VersionDiff],
  template: `<app-version-diff [before]="before" [after]="after" />`,
})
class Host {
  before = 'Summarize: {input}';
  after = 'Summarize concisely: {input}';
}

describe('VersionDiff', () => {
  it('renders removed and added lines for a changed version', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    const text = (fixture.nativeElement.querySelector('[data-testid="diff"]') as HTMLElement)
      .textContent!;
    expect(text).toContain('- Summarize: {input}');
    expect(text).toContain('+ Summarize concisely: {input}');
  });
});
