import type { GitCommit } from '../../../core/repositories/models/repository-history.model';

export interface GraphRow { column: number; width: number; height: number; color: number; segments: string[]; segmentColors: number[]; }
const x = (column: number) => 12 + column * 18;

// Keep lanes through unloaded parents. Recompute over all loaded pages so page
// boundaries never invent a root or disconnect an existing edge.
export function buildCommitGraph(commits: readonly GitCommit[], rowHeight: (commit: GitCommit) => number = () => 44, preferredColors: ReadonlyMap<string, number> = new Map()): GraphRow[] {
  let lanes: string[] = [];
  const colors = new Map<string, number>();
  let nextColor = 0;
  return commits.map(commit => {
    const height = rowHeight(commit); const middle = height / 2;
    const incoming = lanes.includes(commit.sha);
    if (!incoming) { lanes.push(commit.sha); colors.set(commit.sha, preferredColors.get(commit.sha) ?? nextColor); nextColor++; }
    const color = colors.get(commit.sha)!;
    const column = lanes.indexOf(commit.sha);
    const before = [...lanes];
    lanes.splice(column, 1);
    commit.parents.forEach((parent, index) => {
      if (!lanes.includes(parent)) {
        lanes.splice(Math.min(column + index, lanes.length), 0, parent);
        const proposed = index === 0 ? color : nextColor++;
        colors.set(parent, preferredColors.get(parent) ?? proposed);
      }
    });
    const segments: string[] = [];
    const segmentColors: number[] = [];
    before.forEach((sha, index) => {
      if (sha === commit.sha) {
        if (incoming) { segments.push(`M ${x(index)} 0 L ${x(column)} ${middle}`); segmentColors.push(color); }
      } else {
        segments.push(`M ${x(index)} 0 L ${x(index)} ${middle} L ${x(lanes.indexOf(sha))} ${height}`);
        segmentColors.push(colors.get(sha)!);
      }
    });
    commit.parents.forEach(parent => {
      segments.push(`M ${x(column)} ${middle} L ${x(lanes.indexOf(parent))} ${height}`);
      segmentColors.push(colors.get(parent)!);
    });
    return { column, width: Math.max(before.length, lanes.length) * 18 + 6, height, color, segments, segmentColors };
  });
}
