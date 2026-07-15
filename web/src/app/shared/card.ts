import { Component, Directive, contentChild, input } from '@angular/core';

/**
 * Marker for a card's footer-action slot: apply `foot` to any element projected into a
 * {@link Card} and it lands in the `.sb-card__foot` region (e.g. `<button foot>Save</button>`).
 * Consumers import it alongside `Card` — mirrors Angular Material's `MatCardFooter`.
 */
@Directive({ selector: '[foot]' })
export class CardFoot {}

/**
 * The shared surface primitive — standardizes on the brand `.sb-card` (surface, border,
 * large radius, medium shadow, clipped corners) so panels stop hand-rolling boxes. An
 * optional `heading` renders a card header (h3/`--sb-type-h3` treatment); the default slot
 * projects into `.sb-card__body`, and a `[foot]` slot projects footer actions into an
 * `.sb-card__foot` (rendered only when foot content is actually supplied). Foundation only
 * (2.5) — screens adopt it in a later slice.
 */
@Component({
  selector: 'app-card',
  template: `
    <section class="sb-card" data-testid="card">
      @if (heading()) {
        <header class="sb-card__head">
          <h3 class="sb-card__title">{{ heading() }}</h3>
        </header>
      }
      <div class="sb-card__body">
        <ng-content />
      </div>
      @if (foot()) {
        <div class="sb-card__foot">
          <ng-content select="[foot]" />
        </div>
      }
    </section>
  `,
  styles: [
    `
      .sb-card__head {
        padding: var(--sb-space-md) var(--sb-space-lg);
        border-bottom: 1px solid var(--sb-border);
      }
      .sb-card__title {
        margin: 0;
        font-size: var(--sb-type-h3-size);
        font-weight: var(--sb-type-h3-weight);
        color: var(--sb-text);
      }
    `,
  ],
})
export class Card {
  /** Optional card title — omit for a chromeless surface (body only). */
  readonly heading = input('');

  /** Present when a `[foot]` slot is projected — gates the `.sb-card__foot` region. */
  protected readonly foot = contentChild(CardFoot);
}
