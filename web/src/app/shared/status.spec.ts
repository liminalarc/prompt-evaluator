import { originBadge, passBadge } from './status';

describe('status mappers', () => {
  it('passBadge maps pass / fail / unknown to brand variants', () => {
    expect(passBadge(true)).toEqual({ variant: 'success', label: 'Pass' });
    expect(passBadge(false)).toEqual({ variant: 'error', label: 'Fail' });
    expect(passBadge(null)).toEqual({ variant: 'neutral', label: '—' });
  });

  it('originBadge distinguishes captured (info) from synthetic (ai)', () => {
    expect(originBadge('Captured')).toEqual({ variant: 'info', label: 'Captured' });
    expect(originBadge('Synthetic')).toEqual({ variant: 'ai', label: 'Synthetic' });
  });
});
