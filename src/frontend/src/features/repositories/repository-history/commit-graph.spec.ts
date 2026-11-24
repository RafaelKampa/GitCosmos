import { expect, it } from 'vitest';
import { buildCommitGraph } from './commit-graph';
import type { GitCommit } from '../../../core/repositories/models/repository-history.model';
const commit = (sha: string, parents: string[]): GitCommit => ({ sha, parents, author: '', email: '', authoredAt: '', subject: '', refs: '', message: '' });

it('keeps forks, merge edges and parent lanes across loaded pages', () => {
  const first = [commit('merge', ['left', 'right']), commit('left', ['root'])];
  const partial = buildCommitGraph(first);
  const complete = buildCommitGraph([...first, commit('right', ['root']), commit('root', []), commit('separate', [])]);
  expect(complete.slice(0, 2)).toEqual(partial);
  expect(complete[0]!.segments).toHaveLength(2);
  expect(complete[1]!.segments.join(' ')).toContain('M 30 0');
  expect(complete[2]!.column).toBe(1);
  expect(complete[3]!.segments).toEqual(['M 12 0 L 12 22']);
  expect(complete[4]!.segments).toEqual([]);
  expect(buildCommitGraph([])).toEqual([]);
});

it('reuses parents already present in another lane for octopus merges', () => {
  const rows = buildCommitGraph([commit('a', ['b', 'c', 'd']), commit('b', ['c', 'd']), commit('c', ['d']), commit('d', [])]);
  expect(rows[0]!.segments).toHaveLength(3);
  expect(rows[1]!.width).toBe(60);
  expect(rows[3]!.column).toBe(0);
});

it('keeps distinct colors on side lanes and matches each node to its continuing line without scaling coordinates', () => {
  const rows = buildCommitGraph([commit('merge', ['main', 'side']), commit('side', ['base']), commit('main', ['base']), commit('base', [])], item => item.sha === 'merge' ? 56 : 32);
  expect(rows[0]!.height).toBe(56); expect(rows[0]!.segments[0]).toBe('M 12 28 L 12 56');
  expect(rows[1]!.height).toBe(32);
  expect(rows[0]!.color).toBe(rows[2]!.color);
  expect(rows[1]!.color).not.toBe(rows[2]!.color);
  expect(rows[0]!.segmentColors).toEqual([rows[2]!.color, rows[1]!.color]);
  expect(rows[1]!.segments.some(segment => segment.includes('16'))).toBe(true);
});

it('preserves line colors while folding and expanding changes visible lanes', () => {
  const rows = buildCommitGraph([commit('merge', ['main']), commit('main', ['root']), commit('root', [])], () => 32, new Map([['merge', 4], ['main', 4], ['root', 2]]));
  expect(rows.map(row => row.color)).toEqual([4, 4, 2]);
  expect(rows[1]!.segmentColors).toEqual([4, 2]);
});
