export interface LocalGitBranch { name: string; sha: string; upstream: string | null; isCurrent: boolean; isCheckedOut: boolean; }
export interface LocalGitBranches { currentBranch: string; isDetached: boolean; hasCommits: boolean; branches: LocalGitBranch[]; }
export interface RemoteGitBranch { name: string; sha: string; remote: string; branch: string; }
export interface BranchResources { currentBranch: string; operationState: string; remotes: string[]; branches: RemoteGitBranch[]; }
export interface BranchItem extends LocalGitBranch { isRemote: boolean; remote: string; sourceBranch: string; }
export type BranchOperation = 'create' | 'rename' | 'delete' | 'checkout' | 'worktree' | 'fastForward' | 'push' | 'pullRequest' | 'merge' | 'rebase' | 'interactiveRebase' | 'tag' | 'setUpstream' | 'unsetUpstream' | 'fetch' | 'checkoutRemote' | 'deleteRemote' | 'pull' | 'continue' | 'abort';
export interface RebaseStep { sha: string; action: 'pick' | 'drop' | 'squash' | 'fixup'; }
export interface RepositoryBranchRequest { repositoryId: number; workspaceId: number; operation: BranchOperation; name: string; newName: string; isRemote?: boolean; remote?: string; remoteBranch?: string; destination?: string; message?: string; upstream?: string; baseBranch?: string; steps?: RebaseStep[]; confirmation?: string; }
export interface RepositoryBranchPlan { token: string; operation: BranchOperation; name: string; newName: string; sha: string; currentBranch?: string; head?: string; target?: string; targetSha?: string; stateToken?: string; url?: string | null; rewritesHistory?: boolean; published?: boolean; commits?: { sha: string; subject: string }[]; }
