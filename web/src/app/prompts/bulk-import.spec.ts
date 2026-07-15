import { parseBulkImport } from './bulk-import';

describe('parseBulkImport', () => {
  it('parses an array of prompts with versions, defaulting optional fields', () => {
    const result = parseBulkImport(
      JSON.stringify([
        {
          name: 'Summarizer',
          versions: [{ content: 'Summarize: {input}', targetModel: 'claude-sonnet-5' }],
        },
        {
          name: 'Classifier',
          description: 'Buckets tickets',
          versions: [{ content: 'Classify: {input}', targetModel: 'claude-opus-4-8', label: 'v1' }],
        },
      ]),
    );

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.prompts.length).toBe(2);
    expect(result.prompts[0]).toEqual({
      name: 'Summarizer',
      description: null,
      versions: [{ content: 'Summarize: {input}', targetModel: 'claude-sonnet-5', label: null }],
    });
    expect(result.prompts[1].description).toBe('Buckets tickets');
    expect(result.prompts[1].versions[0].label).toBe('v1');
  });

  it('accepts a prompt with no versions', () => {
    const result = parseBulkImport(JSON.stringify([{ name: 'Empty' }]));
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.prompts[0].versions).toEqual([]);
  });

  it('rejects invalid JSON', () => {
    const result = parseBulkImport('{not json');
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toContain('valid JSON');
  });

  it('rejects a non-array top level', () => {
    const result = parseBulkImport(JSON.stringify({ name: 'x' }));
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toContain('array');
  });

  it('rejects an empty array', () => {
    const result = parseBulkImport('[]');
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toContain('No prompts');
  });

  it('rejects a prompt missing a name, naming the row', () => {
    const result = parseBulkImport(JSON.stringify([{ versions: [] }]));
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toContain('#1');
    expect(result.error).toContain('name');
  });

  it('rejects a version missing content or target model, naming the row', () => {
    const result = parseBulkImport(JSON.stringify([{ name: 'X', versions: [{ content: 'hi' }] }]));
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toContain('#1');
    expect(result.error).toContain('target model');
  });
});
