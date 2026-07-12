import { diffLines } from './diff';

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
