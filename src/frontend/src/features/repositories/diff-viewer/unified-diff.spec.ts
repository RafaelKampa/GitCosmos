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
