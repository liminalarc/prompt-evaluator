export type DiffKind = 'context' | 'added' | 'removed';

export interface DiffLine {
  kind: DiffKind;
  text: string;
}

/** A collapsed run of unchanged (context) lines far from any change — the lines are kept so the
 *  UI can reveal them on demand (2.19 W8). */
export interface DiffGap {
  kind: 'gap';
  count: number;
  lines: DiffLine[];
}

/** A row in a collapsed diff: either a real diff line or a collapsible gap of unchanged lines. */
export type DiffRow = DiffLine | DiffGap;

/**
 * GitHub-style collapse of a line diff (2.19 W8): runs of unchanged context far from any change
 * fold into a single gap, so a small edit in a long prompt no longer forces scrolling the entire
 * identical body. Context within `radius` lines of a change stays visible; the rest collapses. A
 * fully-unchanged diff collapses to one gap. The hidden lines ride along so the UI can reveal them.
 */
export function collapseUnchanged(lines: DiffLine[], radius = 3): DiffRow[] {
  // A context line is "near" a change if it's within `radius` lines of any add/remove.
  const near = new Array<boolean>(lines.length).fill(false);
  for (let k = 0; k < lines.length; k++) {
    if (lines[k].kind !== 'context') {
      for (let d = -radius; d <= radius; d++) {
        const idx = k + d;
        if (idx >= 0 && idx < lines.length) near[idx] = true;
      }
    }
  }

  const rows: DiffRow[] = [];
  let i = 0;
  while (i < lines.length) {
    if (lines[i].kind === 'context' && !near[i]) {
      const run: DiffLine[] = [];
      while (i < lines.length && lines[i].kind === 'context' && !near[i]) {
        run.push(lines[i]);
        i++;
      }
      rows.push({ kind: 'gap', count: run.length, lines: run });
    } else {
      rows.push(lines[i]);
      i++;
    }
  }
  return rows;
}

/**
 * Line-level diff of two prompt versions via a longest-common-subsequence backtrace.
 * Registry content is immutable, so this is purely a browsing affordance — no diff library.
 */
export function diffLines(before: string, after: string): DiffLine[] {
  const a = before.split('\n');
  const b = after.split('\n');
  const n = a.length;
  const m = b.length;

  // lcs[i][j] = length of the LCS of a[i..] and b[j..].
  const lcs: number[][] = Array.from({ length: n + 1 }, () => new Array<number>(m + 1).fill(0));
  for (let i = n - 1; i >= 0; i--) {
    for (let j = m - 1; j >= 0; j--) {
      lcs[i][j] = a[i] === b[j] ? lcs[i + 1][j + 1] + 1 : Math.max(lcs[i + 1][j], lcs[i][j + 1]);
    }
  }

  const out: DiffLine[] = [];
  let i = 0;
  let j = 0;
  while (i < n && j < m) {
    if (a[i] === b[j]) {
      out.push({ kind: 'context', text: a[i] });
      i++;
      j++;
    } else if (lcs[i + 1][j] >= lcs[i][j + 1]) {
      out.push({ kind: 'removed', text: a[i] });
      i++;
    } else {
      out.push({ kind: 'added', text: b[j] });
      j++;
    }
  }
  while (i < n) out.push({ kind: 'removed', text: a[i++] });
  while (j < m) out.push({ kind: 'added', text: b[j++] });
  return out;
}
