import { Component, input } from '@angular/core';

/**
 * A brand chip (`.sb-chip`) for neutral categorical tags — scorer kind, target/judge model,
 * source app. Reserve {@link StatusBadge} for colored status; chips are quiet metadata.
 */
@Component({
  selector: 'app-chip',
  template: `<span class="sb-chip" data-testid="chip">{{ label() }}</span>`,
})
export class Chip {
  readonly label = input.required<string>();
}
