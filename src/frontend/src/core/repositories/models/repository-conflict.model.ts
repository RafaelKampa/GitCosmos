export interface ConflictFile { repositoryId: number; workspaceId: number; path: string; }
export interface ConflictVersion { kind: 'absent' | 'binary' | 'text' | 'tooLarge'; text: string; sha: string; mode: string; }
export interface RepositoryConflict { path: string; operation: string; token: string; base: ConflictVersion; ours: ConflictVersion; theirs: ConflictVersion; working: ConflictVersion; }
export type ResolutionAction = 'edit' | 'ours' | 'theirs' | 'delete' | 'stage';
export interface ResolveConflictRequest { file: ConflictFile; action: ResolutionAction; content: string; token: string; }
export interface ConflictResolutionPlan { token: string; path: string; action: ResolutionAction; removesFile: boolean; kind: string; text: string; }
