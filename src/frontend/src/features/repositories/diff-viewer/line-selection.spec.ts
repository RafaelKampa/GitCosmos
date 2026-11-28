import { expect, it } from 'vitest';
import { selectableRows } from './line-selection';
it('retains raw diff indices across headers, CRLF and end-of-file markers', () => {
  const rows = selectableRows('diff --git a/file b/file\n--- a/file\n+++ b/file\n@@ -1 +1 @@\n-old\r\n+new\r\n\\ No newline at end of file\n@@ -5 +5 @@\n context\n');
  expect(rows.filter(row => row.selectable).map(row => row.id)).toEqual([4, 5]);
  expect(rows[0]!.hunk).toBe(-1); expect(rows[7]!.hunk).toBe(1); expect(rows[8]!.selectable).toBe(false);
  expect(selectableRows('+untracked preview')[0]!.selectable).toBe(false); expect(selectableRows('')).toEqual([]);
});
