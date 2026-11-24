import { expect, it } from 'vitest';
import { parseUnifiedDiff } from './unified-diff';

it('keeps metadata, hunk positions, context and both line numbers in a unified diff', () => {
  const result = parseUnifiedDiff('diff --git a/a b/a\n--- a/a\n+++ b/a\n@@ -10,2 +20,2 @@ title\n context\n-old\n+new\n\\ No newline at end of file\n');
  expect(result.map(line => line.type)).toEqual(['meta', 'meta', 'meta', 'hunk', 'context', 'deletion', 'addition', 'meta']);
  expect(result[4]).toMatchObject({ oldLine: 10, newLine: 20 });
  expect(result[5]).toMatchObject({ oldLine: 11, newLine: null });
  expect(result[6]).toMatchObject({ oldLine: null, newLine: 21 });
});

it('supports empty patches, CRLF, multiple hunks and untracked additions without a hunk', () => {
  expect(parseUnifiedDiff('')).toEqual([]);
  const lines = parseUnifiedDiff('@@ -1 +1 @@\r\n-first\r\n+second\r\n@@ -9 +12 @@\r\n context');
  expect(lines[4]).toMatchObject({ oldLine: 9, newLine: 12, text: ' context' });
  expect(parseUnifiedDiff('+new\n+line')).toEqual([
    { type: 'addition', text: '+new', oldLine: null, newLine: 1 },
    { type: 'addition', text: '+line', oldLine: null, newLine: 2 },
  ]);
});

it('preserves code that resembles header markers inside hunks and in untracked previews', () => {
  const patch = 'diff --git a/a b/a\n--- a/a\n+++ b/a\n@@ -1 +1 @@\n--- removed\n+++ added\ndiff --git a/b b/b\n--- a/b\n+++ b/b\n@@ -8 +9 @@\n context';
  const lines = parseUnifiedDiff(patch);
  expect(lines[4]).toMatchObject({ type: 'deletion', text: '--- removed', oldLine: 1 });
  expect(lines[5]).toMatchObject({ type: 'addition', text: '+++ added', newLine: 1 });
  expect(lines[7]!.type).toBe('meta'); expect(lines[8]!.type).toBe('meta');
  expect(lines[10]).toMatchObject({ type: 'context', oldLine: 8, newLine: 9 });
  expect(parseUnifiedDiff('+++ actual file code')[0]).toMatchObject({ type: 'addition', text: '+++ actual file code', newLine: 1 });
});
