import type { FileDiff } from './repository-changes.model';

export interface GitCommit {
  sha: string;
  parents: readonly string[];
  author: string;
  email: string;
  authoredAt: string;
  subject: string;
  refs: string;
  message: string;
}
export interface HistoryPage { commits: readonly GitCommit[]; snapshot: string; hasMore: boolean; }
export interface CommitFile { path: string; originalPath: string | null; status: string; }
export interface CommitDetails { commit: GitCommit; files: readonly CommitFile[]; }
export type CommitDiff = FileDiff;
