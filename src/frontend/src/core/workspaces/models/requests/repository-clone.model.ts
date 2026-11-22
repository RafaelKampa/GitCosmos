export interface PreviewRepositoryCloneRequest { readonly workspaceId: number; readonly remoteUrl: string; readonly parentPath: string; }
export interface CloneWorkspaceRepositoryRequest extends PreviewRepositoryCloneRequest { readonly destinationPath: string; }
export interface RepositoryClonePlan { readonly remoteUrl: string; readonly parentPath: string; readonly folderName: string; readonly destinationPath: string; }
