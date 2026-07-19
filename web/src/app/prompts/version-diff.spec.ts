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

@Component({
  imports: [VersionDiff],
  template: `<app-version-diff [before]="before" [after]="after" />`,
})
class LongHost {
  private readonly body = Array.from({ length: 12 }, (_, i) => `line ${i}`).join('\n');
  before = this.body + '\nold tail';
  after = this.body + '\nnew tail';
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

  it('collapses far unchanged lines into a gap that expands on click [2.19 W8]', () => {
    const fixture = TestBed.createComponent(LongHost);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    // The early identical lines are hidden behind a gap button, not all rendered.
    const gap = el.querySelector('[data-testid="diff-gap"]') as HTMLButtonElement;
    expect(gap).toBeTruthy();
    expect(gap.textContent).toContain('unchanged line');
    expect(el.textContent).not.toContain('line 0');

    // Expanding the gap reveals the hidden lines.
    gap.click();
    fixture.detectChanges();
    expect(el.textContent).toContain('line 0');
    expect(el.querySelector('[data-testid="diff-gap"]')).toBeFalsy();
  });
});
