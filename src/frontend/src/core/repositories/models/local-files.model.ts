import type { DiffArea } from './repository-changes.model';
import type { GitCommit } from './repository-history.model';
import type { CommitBlameLine, CommitExternalTool } from './commit-explorer.model';
export interface LocalFilesRequest { repositoryId: number; workspaceId: number; paths: string[]; area: DiffArea; action: string; token?: string; tool?: string; pattern?: string; start?: number; end?: number; }
export interface LocalFilesView { fullPath: string; patch: string; history: GitCommit[]; blame: CommitBlameLine[]; tools: CommitExternalTool[]; editors: CommitExternalTool[]; }
export interface LocalFilesPlan { token: string; action: string; paths: string[]; preview: string; }
export interface LocalFileContext { path: string; area: DiffArea; paths: string[]; all: string[]; x: number; y: number; }
export interface DiffTextContext { ids: number[]; start: number; end: number; text: string; x: number; y: number; }
