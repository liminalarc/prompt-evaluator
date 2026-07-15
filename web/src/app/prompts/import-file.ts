/**
 * Client-side guard for single-file prompt import (1.6). Pure — no DOM, no FileReader — so the
 * rejection rules are unit-testable in isolation. The add-version form calls this before reading
 * a picked file's text into the existing `content` signal (the import reuses 1.1's POST; there is
 * no new API and no `Prompt` aggregate change).
 */

/** 1 MB — prompts are text; anything larger is almost certainly the wrong file. */
export const MAX_IMPORT_BYTES = 1024 * 1024;

/** Extensions we treat as prompt text when the browser reports no MIME type. */
const TEXT_EXTENSIONS = ['.txt', '.md', '.markdown', '.text', '.prompt'];

export interface ImportValidation {
  ok: boolean;
  /** A user-facing reason, present only when `ok` is false. */
  message?: string;
}

export function validateImportFile(file: File): ImportValidation {
  if (file.size === 0) {
    return { ok: false, message: 'That file is empty — pick a file with prompt text in it.' };
  }
  if (file.size > MAX_IMPORT_BYTES) {
    return { ok: false, message: 'That file is too large — prompt imports are capped at 1 MB.' };
  }
  if (!looksLikeText(file)) {
    return {
      ok: false,
      message: 'That doesn’t look like a text file — import a .txt, .md, or .prompt file.',
    };
  }
  return { ok: true };
}

function looksLikeText(file: File): boolean {
  const type = file.type;
  // An explicit MIME type is authoritative: accept text/*, reject everything else (image/*, etc.).
  if (type) {
    return type.startsWith('text/');
  }
  // No MIME type (common for .md/.prompt): fall back to the extension allow-list.
  const name = file.name.toLowerCase();
  return TEXT_EXTENSIONS.some((ext) => name.endsWith(ext));
}
