import { expect, it } from 'vitest';
import { buildFileTree } from './file-tree';
import type { GitFileChange } from '../../../core/repositories/models/repository-changes.model';
const file = (path: string): GitFileChange => ({ path, originalPath: null, indexStatus: '.', workTreeStatus: '?', isUntracked: true, isConflicted: false, isSubmodule: false });
it('groups nested paths into folders, sorts folders first and preserves full literal paths', () => {
  const files = [file('z.txt'), file('src/core/b.ts'), file('src/core/a.ts'), file('a.txt'), file('área/[file].txt'), file('src/core/a.ts')];
  const tree = buildFileTree(files);
  expect(tree.map(node => node.name)).toEqual(['área', 'src', 'a.txt', 'z.txt']);
  const core = tree[1]!.children[0]!;
  expect(core.children.map(node => node.name)).toEqual(['a.ts', 'b.ts']);
  expect(core.children[0]!.file?.path).toBe('src/core/a.ts');
  expect(tree[0]!.children[0]!.file?.path).toBe('área/[file].txt');
  expect(buildFileTree([])).toEqual([]);
});

it('keeps both a removed file and the new folder replacing its path', () => {
  const tree = buildFileTree([{ ...file('src'), workTreeStatus: 'D', isUntracked: false }, file('src/new.txt')]);
  expect(tree).toHaveLength(2);
  expect(tree[0]!.file).toBeNull(); expect(tree[0]!.children[0]!.file?.path).toBe('src/new.txt');
  expect(tree[1]!.file?.workTreeStatus).toBe('D');
});
