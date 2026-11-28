import { expect, it } from 'vitest';
import { visibleChangeNodes, selectTreeItems, selectedFilePaths, expandSelectedKeys } from './tree-selection';
import { buildFileTree } from './file-tree';
it('selects the entire visible interval with Shift and toggles only clicked items with Ctrl', () => {
  const keys = ['d:a', 'f:a/1', 'f:a/2', 'f:b', 'f:c'];
  expect(selectTreeItems(keys, ['f:c'], 'f:a/1', 'f:b', true, false)).toEqual(['f:a/1', 'f:a/2', 'f:b']);
  expect(selectTreeItems(keys, [], 'f:b', 'f:a/1', true, false)).toEqual(['f:a/1', 'f:a/2', 'f:b']);
  expect(selectTreeItems(keys, ['f:c'], 'f:a/1', 'f:b', true, true)).toEqual(['f:c', 'f:a/1', 'f:a/2', 'f:b']);
  expect(selectTreeItems(keys, ['f:c'], null, 'f:b', false, true)).toEqual(['f:c', 'f:b']);
  expect(selectTreeItems(keys, ['f:c', 'f:b'], null, 'f:b', false, true)).toEqual(['f:c']);
  expect(selectTreeItems(keys, ['f:c'], 'missing', 'f:b', true, false)).toEqual(['f:b']);
});
it('expands folder selections to descendants even when collapsed and deduplicates file paths', () => {
  const base = { originalPath: null, indexStatus: 'M', workTreeStatus: 'M', isUntracked: false, isConflicted: false, isSubmodule: false };
  const tree = buildFileTree([{ ...base, path: 'a/deep/1' }, { ...base, path: 'a/2' }, { ...base, path: 'b' }]);
  expect(visibleChangeNodes(tree, ['a']).map(row => row.key)).toEqual(['d:a', 'f:b']);
  expect(visibleChangeNodes(tree, []).map(row => row.depth)).toEqual([0, 1, 2, 1, 0]);
  const keys = expandSelectedKeys(['d:a', 'f:a/2'], tree); expect(keys).toEqual(['d:a', 'd:a/deep', 'f:a/deep/1', 'f:a/2']);
  expect(selectedFilePaths(keys, ['a/deep/1', 'a/2', 'b'])).toEqual(['a/deep/1', 'a/2']);
});
