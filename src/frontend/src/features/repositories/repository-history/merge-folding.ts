import type { GitCommit } from '../../../core/repositories/models/repository-history.model';

export interface FoldedHistory { commits: GitCommit[]; graphCommits: GitCommit[]; hidden: Set<string>; }

// A merge's interior is reachable from its secondary parents, excluding the
// entire ancestry of its first parent. Work only with the loaded topology.
export function foldMergeHistory(commits: readonly GitCommit[], collapsed: ReadonlySet<string>): FoldedHistory {
  const index = new Map(commits.map(commit => [commit.sha, commit]));
  function ancestors(starts: readonly string[]) {
    const visited = new Set<string>(); const pending = [...starts];
    while (pending.length) {
      const sha = pending.pop()!;
      if (visited.has(sha)) continue;
      visited.add(sha);
      const commit = index.get(sha);
      if (commit) pending.push(...commit.parents);
    }
    return visited;
  }
  const hidden = new Set<string>();
  for (const sha of collapsed) {
    const merge = index.get(sha);
    if (!merge || merge.parents.length < 2) continue;
    const main = ancestors(merge.parents.slice(0, 1));
    for (const candidate of ancestors(merge.parents.slice(1))) {
      if (!main.has(candidate) && index.has(candidate) && candidate !== sha) hidden.add(candidate);
    }
  }
  const visible = commits.filter(commit => !hidden.has(commit.sha));
  const graphCommits = visible.map(commit => {
    const pending = collapsed.has(commit.sha) ? commit.parents.slice(0, 1) : [...commit.parents];
    const parents = new Set<string>(); const visited = new Set<string>();
    while (pending.length) {
      const sha = pending.shift()!;
      if (visited.has(sha)) continue;
      visited.add(sha);
      if (hidden.has(sha)) pending.push(...index.get(sha)!.parents);
      else parents.add(sha);
    }
    return { ...commit, parents: [...parents] };
  });
  return { commits: visible, graphCommits, hidden };
}
