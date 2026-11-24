import { ApiBaseService } from '../api/api-base-service';
import type { HistoryPage, CommitDetails, CommitDiff } from './models/repository-history.model';

export class RepositoryHistoryApiService extends ApiBaseService {
  getHistory(workspaceId: number, repositoryId: number, page: number, snapshot: string | null, signal?: AbortSignal) {
    const query = new URLSearchParams({ workspaceId: String(workspaceId), repositoryId: String(repositoryId), page: String(page), pageSize: '50' });
    if (snapshot !== null) query.set('snapshot', snapshot);
    return this.get<HistoryPage>('/api/RepositoryHistory/GetHistory?' + query, signal);
  }
  getCommit(workspaceId: number, repositoryId: number, sha: string, signal?: AbortSignal) {
    return this.get<CommitDetails>('/api/RepositoryHistory/GetCommit?' + new URLSearchParams({ workspaceId: String(workspaceId), repositoryId: String(repositoryId), sha }), signal);
  }
  getDiff(workspaceId: number, repositoryId: number, sha: string, path: string, signal?: AbortSignal) {
    return this.get<CommitDiff>('/api/RepositoryHistory/GetDiff?' + new URLSearchParams({ workspaceId: String(workspaceId), repositoryId: String(repositoryId), sha, path }), signal);
  }
}
