import { DiffGap, collapseUnchanged, diffLines } from './diff';

describe('diffLines', () => {
  it('marks identical text as all context', () => {
    const result = diffLines('a\nb\nc', 'a\nb\nc');
    expect(result.map((l) => l.kind)).toEqual(['context', 'context', 'context']);
  });

  it('detects an added line', () => {
    const result = diffLines('a\nc', 'a\nb\nc');
    expect(result).toEqual([
      { kind: 'context', text: 'a' },
      { kind: 'added', text: 'b' },
      { kind: 'context', text: 'c' },
    ]);
  });

  it('detects a removed line', () => {
    const result = diffLines('a\nb\nc', 'a\nc');
    expect(result).toEqual([
      { kind: 'context', text: 'a' },
      { kind: 'removed', text: 'b' },
      { kind: 'context', text: 'c' },
    ]);
  });

  it('represents a changed line as a removal followed by an addition', () => {
    const result = diffLines('Summarize: {input}', 'Summarize concisely: {input}');
    expect(result).toEqual([
      { kind: 'removed', text: 'Summarize: {input}' },
      { kind: 'added', text: 'Summarize concisely: {input}' },
    ]);
  });
});

describe('collapseUnchanged [2.19 W8]', () => {
  it('keeps context near a change and collapses the far run into a gap', () => {
    // 10 identical lines, then a change on line 11 — the early identical run collapses.
    const before = Array.from({ length: 10 }, (_, i) => `line ${i}`).join('\n') + '\nold';
    const after = Array.from({ length: 10 }, (_, i) => `line ${i}`).join('\n') + '\nnew';
    const rows = collapseUnchanged(diffLines(before, after), 3);

    // First row is the gap for the far-from-change lines; a gap carries its hidden lines.
    const gap = rows[0] as DiffGap;
    expect(gap.kind).toBe('gap');
    expect(gap.count).toBe(gap.lines.length);
    expect(gap.count).toBeGreaterThan(0);
    // The change and its surrounding context remain as real diff rows (not collapsed).
    const kinds = rows.map((r) => r.kind);
    expect(kinds).toContain('removed');
    expect(kinds).toContain('added');
    // Exactly the last `radius` context lines before the change stay visible.
    const visibleContext = rows.filter((r) => r.kind === 'context').length;
    expect(visibleContext).toBe(3);
  });

  it('collapses a fully-unchanged diff into a single gap', () => {
    const rows = collapseUnchanged(diffLines('a\nb\nc\nd\ne\nf\ng', 'a\nb\nc\nd\ne\nf\ng'), 3);
    expect(rows.length).toBe(1);
    expect(rows[0].kind).toBe('gap');
    expect((rows[0] as DiffGap).count).toBe(7);
  });

  it('leaves a small diff uncollapsed (no gap when everything is near a change)', () => {
    const rows = collapseUnchanged(diffLines('a\nc', 'a\nb\nc'), 3);
    expect(rows.every((r) => r.kind !== 'gap')).toBeTrue();
  });
});
