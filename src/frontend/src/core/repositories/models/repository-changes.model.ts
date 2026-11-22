import type { GitWorkingTreeStatus } from '../../workspaces/models/responses/workspace-dashboard-response.model';

export type DiffArea = 'staged' | 'working';
export interface GitFileChange {
  path: string;
  originalPath: string | null;
  indexStatus: string;
  workTreeStatus: string;
  isUntracked: boolean;
  isConflicted: boolean;
  isSubmodule: boolean;
}
export interface RepositoryChanges {
  id: number;
  name: string;
  localPath: string;
  status: GitWorkingTreeStatus;
  files: readonly GitFileChange[];
}
export interface DiffSelection { path: string; area: DiffArea; }
export interface FileDiff extends DiffSelection {
  kind: 'text' | 'empty' | 'binary' | 'tooLarge' | 'submodule' | 'conflict';
  patch: string;
}
