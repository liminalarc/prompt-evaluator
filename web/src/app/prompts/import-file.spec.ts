import { validateImportFile } from './import-file';

// Builds a File with a controllable size without allocating megabytes of real content: the size
// getter is what validation reads, so we stub it while keeping a real File for type/name.
function fileOf(name: string, type: string, size: number): File {
  const file = new File(['x'], name, { type });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

describe('validateImportFile', () => {
  it('accepts a small text/* file', () => {
    expect(validateImportFile(fileOf('prompt.txt', 'text/plain', 42)).ok).toBe(true);
  });

  it('accepts a markdown file by extension when the browser gives no MIME type', () => {
    expect(validateImportFile(fileOf('prompt.md', '', 42)).ok).toBe(true);
  });

  it('accepts a .prompt file by extension', () => {
    expect(validateImportFile(fileOf('system.prompt', '', 42)).ok).toBe(true);
  });

  it('rejects an empty file with a clear message', () => {
    const result = validateImportFile(fileOf('empty.txt', 'text/plain', 0));
    expect(result.ok).toBe(false);
    expect(result.message).toContain('empty');
  });

  it('rejects a file over 1 MB with a clear message', () => {
    const result = validateImportFile(fileOf('huge.txt', 'text/plain', 1024 * 1024 + 1));
    expect(result.ok).toBe(false);
    expect(result.message).toContain('large');
  });

  it('rejects a binary file (explicit non-text MIME) with a clear message', () => {
    const result = validateImportFile(fileOf('logo.png', 'image/png', 42));
    expect(result.ok).toBe(false);
    expect(result.message).toContain('text');
  });

  it('rejects an unknown-extension file with no MIME type', () => {
    const result = validateImportFile(fileOf('mystery.bin', '', 42));
    expect(result.ok).toBe(false);
    expect(result.message).toContain('text');
  });
});
