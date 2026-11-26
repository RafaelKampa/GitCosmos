export interface CommitFileSelection { repositoryId: number; workspaceId: number; sha: string; path: string; }
export interface CommitTreeEntry { path: string; mode: string; sha: string; }
export interface CommitFileContent { path: string; kind: 'text' | 'binary' | 'submodule'; text: string; base64: string; fullPath: string; }
export interface CommitBlameLine { line: number; sha: string; author: string; time: string; text: string; }
export interface CommitRestorePlan { token: string; path: string; sourceSha: string; sourcePath: string; exists: boolean; }
export interface CommitExternalTool { id: string; name: string; }
