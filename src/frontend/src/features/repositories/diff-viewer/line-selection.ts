import { parseUnifiedDiff } from './unified-diff';
export function selectableRows(patch: string) {
  let hunk = -1;
  return parseUnifiedDiff(patch).map((line, id) => {
    if (line.type === 'hunk') hunk++;
    return { ...line, id, hunk, selectable: hunk >= 0 && (line.type === 'addition' || line.type === 'deletion') };
  });
}
