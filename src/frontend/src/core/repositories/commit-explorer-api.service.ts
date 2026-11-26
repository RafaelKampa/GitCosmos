import { ApiBaseService } from '../api/api-base-service';
import type { CommitFileSelection, CommitTreeEntry, CommitFileContent, CommitBlameLine, CommitRestorePlan, CommitExternalTool } from './models/commit-explorer.model';
import type { GitCommit } from './models/repository-history.model';
export class CommitExplorerApiService extends ApiBaseService {
  private query(file: CommitFileSelection) { return new URLSearchParams({ repositoryId: String(file.repositoryId), workspaceId: String(file.workspaceId), sha: file.sha, path: file.path }); }
  tree(file: CommitFileSelection, signal?: AbortSignal) { return this.get<CommitTreeEntry[]>('/api/CommitExplorer/Tree?' + this.query(file), signal); }
  file(file: CommitFileSelection, signal?: AbortSignal) { return this.get<CommitFileContent>('/api/CommitExplorer/File?' + this.query(file), signal); }
  history(file: CommitFileSelection, signal?: AbortSignal) { return this.get<GitCommit[]>('/api/CommitExplorer/History?' + this.query(file), signal); }
  blame(file: CommitFileSelection, signal?: AbortSignal) { return this.get<CommitBlameLine[]>('/api/CommitExplorer/Blame?' + this.query(file), signal); }
  tools(file: CommitFileSelection, signal?: AbortSignal) { return this.get<CommitExternalTool[]>('/api/CommitExplorer/Tools?' + this.query(file), signal); }
  prepare(file: CommitFileSelection, before: boolean, signal?: AbortSignal) { return this.post<CommitRestorePlan>('/api/CommitExplorer/PrepareRestore', { file, before }, signal); }
  restore(file: CommitFileSelection, before: boolean, token: string, signal?: AbortSignal) { return this.post<boolean>('/api/CommitExplorer/Restore', { file, before, token }, signal); }
  desktop(file: CommitFileSelection, action: string, tool: string, signal?: AbortSignal) { return this.post<boolean>('/api/CommitExplorer/Desktop', { file, action, tool }, signal); }
}
