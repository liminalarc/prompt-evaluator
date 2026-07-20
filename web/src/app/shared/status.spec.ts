import { originBadge, passBadge, versionStatusBadges } from './status';

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

  // 1.16 — only the single Backport target is badged, not every eligible version.
  it('versionStatusBadges badges Current, the Backport target, and Regressed', () => {
    expect(
      versionStatusBadges({ isCurrent: true, isBackportTarget: false, regressed: false }),
    ).toEqual([{ variant: 'info', label: 'Current' }]);
    expect(
      versionStatusBadges({ isCurrent: false, isBackportTarget: true, regressed: false }),
    ).toEqual([{ variant: 'success', label: 'Backport target' }]);
    // A version that merely beats Current but isn't the target carries no badge.
    expect(
      versionStatusBadges({ isCurrent: false, isBackportTarget: false, regressed: false }),
    ).toEqual([]);
    expect(
      versionStatusBadges({ isCurrent: false, isBackportTarget: false, regressed: true }),
    ).toEqual([{ variant: 'error', label: 'Regressed' }]);
  });
});
