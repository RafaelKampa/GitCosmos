import { expect, it } from 'vitest';
import { foldMergeHistory } from './merge-folding';
import type { GitCommit } from '../../../core/repositories/models/repository-history.model';
const commit = (sha: string, parents: string[]): GitCommit => ({ sha, parents, refs: '', author: '', email: '', authoredAt: '', subject: sha, message: '' });

it('hides only side ancestry of a merge and reconnects visible external descendants through hidden commits', () => {
  const commits = [commit('merge', ['main', 'side']), commit('external', ['side']), commit('side', ['nested', 'main']), commit('nested', ['root']), commit('main', ['root']), commit('root', [])];
  const folded = foldMergeHistory(commits, new Set(['merge']));
  expect([...folded.hidden].sort()).toEqual(['nested', 'side']);
  expect(folded.commits.map(item => item.sha)).toEqual(['merge', 'external', 'main', 'root']);
  expect(folded.graphCommits[0]!.parents).toEqual(['main']);
  expect(folded.graphCommits[1]!.parents).toEqual(['main', 'root']);
  expect(foldMergeHistory(commits, new Set()).commits).toEqual(commits);
  expect(commits[0]!.parents).toEqual(['main', 'side']);
});

it('preserves nested folds and handles octopus merges, shared ancestors and unloaded parents', () => {
  const commits = [commit('outer', ['main', 'inner', 'extra']), commit('inner', ['side', 'nested']), commit('extra', ['root']), commit('nested', ['root']), commit('side', ['root']), commit('main', ['root']), commit('root', [])];
  const nested = new Set(['inner']);
  expect(foldMergeHistory(commits, nested).commits.map(item => item.sha)).not.toContain('nested');
  nested.add('outer'); expect(foldMergeHistory(commits, nested).commits.map(item => item.sha)).toEqual(['outer', 'main', 'root']);
  nested.delete('outer'); expect(foldMergeHistory(commits, nested).commits.map(item => item.sha)).toContain('inner');
  expect(foldMergeHistory(commits, nested).commits.map(item => item.sha)).not.toContain('nested');
  expect(foldMergeHistory(commits.slice(0, 1), new Set(['outer'])).graphCommits[0]!.parents).toEqual(['main']);
  expect(foldMergeHistory(commits, new Set(['absent', 'root'])).hidden.size).toBe(0);
});

it('terminates when ancestry is shared or malformed and does not hide the merge itself', () => {
  const commits = [commit('merge', ['main', 'cycle']), commit('main', []), commit('cycle', ['cycle', 'merge']), commit('external', ['cycle'])];
  const folded = foldMergeHistory(commits, new Set(['merge']));
  expect(folded.commits.map(item => item.sha)).toEqual(['merge', 'main', 'external']);
  expect(folded.graphCommits[2]!.parents).toEqual(['merge']);
});
