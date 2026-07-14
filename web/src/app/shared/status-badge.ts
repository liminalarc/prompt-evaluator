import { Component, input } from '@angular/core';
import { BadgeVariant } from './status';

/**
 * A brand status badge (`.sb-badge`) — the single place raw pass/fail/severity glyphs and
 * plain-text status get rendered as a colored pill. Dumb: variant + label in.
 */
@Component({
  selector: 'app-status-badge',
  template: `<span class="sb-badge" [class]="'sb-badge--' + variant()" data-testid="status-badge">{{
    label()
  }}</span>`,
})
export class StatusBadge {
  readonly variant = input<BadgeVariant>('neutral');
  readonly label = input.required<string>();
}
