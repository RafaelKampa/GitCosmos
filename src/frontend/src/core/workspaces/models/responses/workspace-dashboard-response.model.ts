export interface GitWorkingTreeStatus {
  branch: string;
  upstream?: string | null;
  ahead?: number | null;
  behind?: number | null;
  isDetached: boolean;
  hasCommits: boolean;
  changedFiles: number;
  modified: number;
  added: number;
  deleted: number;
  renamed: number;
  untracked: number;
  conflicted: number;
  staged: number;
  unstaged: number;
}
export interface RepositoryDashboardResponse {
  id: number;
  name: string;
  localPath: string;
  state: 'clean' | 'modified' | 'conflicted' | 'unavailable';
  status: GitWorkingTreeStatus | null;
  error: string | null;
}
export interface WorkspaceDashboardResponse {
  workspaceId: number;
  name: string;
  checkedAt: string;
  repositories: readonly RepositoryDashboardResponse[];
}
