/**
 * Parser + schema validation for bulk prompt import (1.6). Pure — no HTTP — so the whole schema
 * is unit-testable. The `/prompts` import action feeds this a file's text, then orchestrates the
 * import **client-side** by looping the existing 1.1 POSTs (createPrompt + addVersion) over the
 * result: there is no server-side batch endpoint and no `Prompt` aggregate change.
 *
 * Locked JSON shape — an array of prompts, each with optional description and a `versions[]`:
 *
 *   [
 *     { "name": "Summarizer", "description": null,
 *       "versions": [
 *         { "content": "Summarize: {input}", "targetModel": "claude-sonnet-5", "label": null }
 *       ] }
 *   ]
 *
 * A row's folder is not part of the shape — bulk import files into the org + folder currently in
 * view (mirrors single-prompt create).
 */

export interface BulkVersion {
  content: string;
  targetModel: string;
  label: string | null;
}

export interface BulkPrompt {
  name: string;
  description: string | null;
  versions: BulkVersion[];
}

export type BulkParseResult = { ok: true; prompts: BulkPrompt[] } | { ok: false; error: string };

export function parseBulkImport(text: string): BulkParseResult {
  let raw: unknown;
  try {
    raw = JSON.parse(text);
  } catch {
    return { ok: false, error: 'That file isn’t valid JSON.' };
  }

  if (!Array.isArray(raw)) {
    return { ok: false, error: 'Expected a JSON array of prompts.' };
  }
  if (raw.length === 0) {
    return { ok: false, error: 'No prompts found in the file.' };
  }

  const prompts: BulkPrompt[] = [];
  for (let i = 0; i < raw.length; i++) {
    const row = raw[i] as Record<string, unknown>;
    const at = `Prompt #${i + 1}`;

    if (typeof row !== 'object' || row === null) {
      return { ok: false, error: `${at} is not an object.` };
    }
    const name = row['name'];
    const description = row['description'];
    const rawVersions = row['versions'];
    if (typeof name !== 'string' || name.trim() === '') {
      return { ok: false, error: `${at} is missing a name.` };
    }
    if (description != null && typeof description !== 'string') {
      return { ok: false, error: `${at} has an invalid description.` };
    }
    if (rawVersions != null && !Array.isArray(rawVersions)) {
      return { ok: false, error: `${at} has an invalid versions list.` };
    }

    const versions: BulkVersion[] = [];
    const versionRows: unknown[] = rawVersions ?? [];
    for (let j = 0; j < versionRows.length; j++) {
      const v = versionRows[j] as Record<string, unknown>;
      const vAt = `${at}, version #${j + 1}`;
      if (typeof v !== 'object' || v === null) {
        return { ok: false, error: `${vAt} is not an object.` };
      }
      const content = v['content'];
      const targetModel = v['targetModel'];
      const label = v['label'];
      if (
        typeof content !== 'string' ||
        content.trim() === '' ||
        typeof targetModel !== 'string' ||
        targetModel.trim() === ''
      ) {
        return { ok: false, error: `${vAt} needs content and a target model.` };
      }
      if (label != null && typeof label !== 'string') {
        return { ok: false, error: `${vAt} has an invalid label.` };
      }
      versions.push({ content, targetModel, label: label ?? null });
    }

    prompts.push({ name: name.trim(), description: description ?? null, versions });
  }

  return { ok: true, prompts };
}
