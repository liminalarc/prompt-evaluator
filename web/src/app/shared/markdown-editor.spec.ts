import { TestBed } from '@angular/core/testing';
import { MarkdownEditor } from './markdown-editor';

describe('MarkdownEditor (2.10)', () => {
  interface XssWindow extends Window {
    __xss?: boolean;
  }

  function render(value: string, mode: 'edit' | 'preview' = 'edit') {
    TestBed.configureTestingModule({ imports: [MarkdownEditor] });
    const fixture = TestBed.createComponent(MarkdownEditor);
    fixture.componentRef.setInput('value', value);
    if (mode === 'preview') {
      (fixture.componentInstance as unknown as { mode: { set(v: string): void } }).mode.set(
        'preview',
      );
    }
    fixture.detectChanges();
    return fixture;
  }

  it('renders the source text in an editable textarea by default', () => {
    const fixture = render('## Heading\n\n- one\n- two', 'edit');
    const textarea = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    expect(textarea).not.toBeNull();
    expect(textarea.value).toBe('## Heading\n\n- one\n- two');
    // No rendered preview while editing.
    expect(fixture.nativeElement.querySelector('[data-testid="md-preview"]')).toBeNull();
  });

  it('emits typed source unchanged (source is authoritative, preview is display-only)', () => {
    const fixture = render('', 'edit');
    let emitted = '';
    fixture.componentInstance.valueChange.subscribe((v) => (emitted = v));
    const textarea = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    textarea.value = '# Title\n\nBody **bold**.';
    textarea.dispatchEvent(new Event('input'));
    expect(emitted).toBe('# Title\n\nBody **bold**.');
  });

  it('renders markdown structure in Preview mode', () => {
    const fixture = render('## Section\n\n- alpha\n- beta', 'preview');
    const preview = fixture.nativeElement.querySelector(
      '[data-testid="md-preview"]',
    ) as HTMLElement;
    expect(preview.querySelector('h2')).not.toBeNull();
    expect(preview.querySelectorAll('li').length).toBe(2);
  });

  it('renders a sanitized, inert preview for an XSS payload [2.10]', () => {
    (window as XssWindow).__xss = undefined;
    // The javascript: link is a clean markdown link (own paragraph) so marked renders a real anchor
    // — proving DOMPurify strips the dangerous href; the raw HTML vectors follow as blocks.
    const payload = [
      '[click me](javascript:window.__xss=true)',
      '',
      '# Title',
      '',
      '<script>window.__xss = true;</script>',
      '',
      '<img src="x" onerror="window.__xss = true;">',
    ].join('\n');
    const fixture = render(payload, 'preview');
    const preview = fixture.nativeElement.querySelector(
      '[data-testid="md-preview"]',
    ) as HTMLElement;

    // The heading still renders (markdown works) …
    expect(preview.querySelector('h1')).not.toBeNull();
    // … but every script vector is stripped and nothing executed.
    expect(preview.querySelector('script')).toBeNull();
    expect(preview.innerHTML).not.toContain('onerror');
    // The anchor survives as text but its javascript: href is removed by DOMPurify.
    const hrefs = Array.from(preview.querySelectorAll('a')).map((a) =>
      (a.getAttribute('href') ?? '').toLowerCase(),
    );
    expect(hrefs.some((h) => h.includes('javascript:'))).toBe(false);
    expect(preview.innerHTML.toLowerCase()).not.toContain('javascript:');
    expect((window as XssWindow).__xss).toBeUndefined();
  });
});
