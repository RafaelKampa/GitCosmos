export interface RepositoryEditRequest { repositoryId: number; workspaceId: number; operation: 'stage' | 'unstage' | 'commit'; paths: string[]; subject: string; description: string; amend: boolean; push: boolean; }
export interface RepositoryEditPlan { token: string; operation: string; paths: readonly string[]; fileCount: number; head: string; amend: boolean; published: boolean; pushDestination: string | null; }
export interface RepositoryEditResult { operation: string; head: string; committed: boolean; pushAttempted: boolean; pushSucceeded: boolean; warning: string | null; }
export interface MoveFilesRequest { operation: 'stage' | 'unstage'; paths: string[]; }
