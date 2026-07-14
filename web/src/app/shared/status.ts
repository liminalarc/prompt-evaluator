/** The brand badge variants (`.sb-badge--*`) — status colors are reserved for these mappers. */
export type BadgeVariant =
  'primary' | 'accent' | 'ai' | 'info' | 'neutral' | 'success' | 'warn' | 'error';

/** A badge to render: its brand variant + the text it carries. */
export interface BadgeSpec {
  variant: BadgeVariant;
  label: string;
}

/** Pass / fail / not-applicable for a score, as a colored badge (was raw ✅/❌/—). */
export function passBadge(passed: boolean | null): BadgeSpec {
  if (passed === null) return { variant: 'neutral', label: '—' };
  return passed ? { variant: 'success', label: 'Pass' } : { variant: 'error', label: 'Fail' };
}

/** Fixture origin as a badge — captured ground-truth vs. AI-synthesized coverage. */
export function originBadge(origin: 'Captured' | 'Synthetic'): BadgeSpec {
  return origin === 'Captured'
    ? { variant: 'info', label: 'Captured' }
    : { variant: 'ai', label: 'Synthetic' };
}
