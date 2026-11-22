export interface DiffLine {
  type: 'meta' | 'hunk' | 'addition' | 'deletion' | 'context';
  text: string;
  oldLine: number | null;
  newLine: number | null;
}

export function parseUnifiedDiff(patch: string): DiffLine[] {
  if (!patch) return [];
  let oldLine = 1; let newLine = 1;
  const lines = patch.replace(/\r\n/g, '\n').split('\n');
  if (lines[lines.length - 1] === '') lines.pop();
  return lines.map(text => {
    const hunk = /^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@/.exec(text);
    if (hunk) {
      oldLine = Number(hunk[1]); newLine = Number(hunk[2]);
      return { type: 'hunk', text, oldLine: null, newLine: null };
    }
    if (text.startsWith('+++ ') || text.startsWith('--- ') || (!text.startsWith('+') && !text.startsWith('-') && !text.startsWith(' ')))
      return { type: 'meta', text, oldLine: null, newLine: null };
    if (text.startsWith('+')) return { type: 'addition', text, oldLine: null, newLine: newLine++ };
    if (text.startsWith('-')) return { type: 'deletion', text, oldLine: oldLine++, newLine: null };
    return { type: 'context', text, oldLine: oldLine++, newLine: newLine++ };
  });
}
