import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { marked } from 'marked';
import DOMPurify from 'dompurify';

/**
 * Reusable markdown editor (2.10): a source `<textarea>` (the authoritative text) with an
 * **Edit ⇄ Preview** toggle. Preview renders the markdown to HTML for reading only — the raw text
 * is never mutated by the preview.
 *
 * XSS-safe by construction: the preview HTML is produced by a vetted renderer (`marked`) then
 * **sanitized by DOMPurify**, and bound via `[innerHTML]` so Angular's built-in sanitizer runs on
 * it as well (defense in depth). A `<script>`/`onerror`/`javascript:` payload in the source is
 * stripped and never executes.
 */
@Component({
  selector: 'app-markdown-editor',
  template: `
    <div class="md-editor">
      <div class="md-editor__tabs" role="tablist">
        <button
          type="button"
          class="md-tab"
          role="tab"
          [class.md-tab--active]="mode() === 'edit'"
          [attr.aria-selected]="mode() === 'edit'"
          data-testid="md-edit-tab"
          (click)="mode.set('edit')"
        >
          Edit
        </button>
        <button
          type="button"
          class="md-tab"
          role="tab"
          [class.md-tab--active]="mode() === 'preview'"
          [attr.aria-selected]="mode() === 'preview'"
          data-testid="md-preview-tab"
          (click)="mode.set('preview')"
        >
          Preview
        </button>
      </div>

      @if (mode() === 'edit') {
        <textarea
          [id]="inputId || null"
          [attr.name]="name || null"
          [attr.rows]="rows"
          [attr.data-testid]="testid || null"
          [attr.placeholder]="placeholder || null"
          [value]="value()"
          (input)="onInput($event)"
        ></textarea>
      } @else {
        <div class="md-preview" data-testid="md-preview" [innerHTML]="rendered()"></div>
      }
    </div>
  `,
  styles: [
    `
      .md-editor {
        display: flex;
        flex-direction: column;
        gap: var(--sb-space-xs);
      }
      .md-editor__tabs {
        display: flex;
        gap: var(--sb-space-xs);
      }
      .md-tab {
        border: 1px solid var(--sb-border);
        background: var(--sb-surface);
        color: var(--sb-text-secondary);
        padding: 2px var(--sb-space-sm);
        border-radius: var(--sb-radius-sm);
        cursor: pointer;
        font-size: var(--sb-type-small-size);
      }
      .md-tab--active {
        background: var(--sb-primary-surface);
        border-color: var(--sb-primary);
        color: var(--sb-primary);
        font-weight: 600;
      }
      /* W3/W21: a roomy, monospace *source* editor — prompts/rubrics are raw text the model reads,
         so exact source fidelity matters (no proportional font, no reflow). Tall by default and
         resizable, so a ~55-line prompt isn't wrestled through an 8-row box. */
      .md-editor textarea {
        width: 100%;
        box-sizing: border-box;
        resize: vertical;
        min-height: 12rem;
        font-family: var(--sb-font-mono);
        font-size: var(--sb-type-small-size);
        line-height: 1.5;
        tab-size: 2;
      }
      .md-preview {
        border: 1px solid var(--sb-border);
        border-radius: var(--sb-radius-sm);
        padding: var(--sb-space-md);
        background: var(--sb-surface-raised);
        min-height: 6rem;
        overflow-x: auto;
      }
      .md-preview > :first-child {
        margin-top: 0;
      }
      .md-preview > :last-child {
        margin-bottom: 0;
      }
      .md-preview pre {
        overflow-x: auto;
        padding: var(--sb-space-sm);
        background: var(--sb-surface);
        border-radius: var(--sb-radius-sm);
      }
    `,
  ],
})
export class MarkdownEditor {
  protected readonly mode = signal<'edit' | 'preview'>('edit');
  protected readonly value = signal('');

  @Input('value') set valueInput(v: string) {
    this.value.set(v ?? '');
  }
  @Input() inputId = '';
  @Input() name = '';
  @Input() rows = 6;
  @Input() testid = '';
  @Input() placeholder = '';
  @Output() valueChange = new EventEmitter<string>();

  // marked (sync) → DOMPurify. Returned as a plain string so Angular's [innerHTML] sanitizer also
  // runs on it. Source text is never touched — this is display-only.
  protected readonly rendered = computed<string>(() => {
    const html = marked.parse(this.value(), { async: false }) as string;
    return DOMPurify.sanitize(html);
  });

  protected onInput(event: Event): void {
    const next = (event.target as HTMLTextAreaElement).value;
    this.value.set(next);
    this.valueChange.emit(next);
  }
}
