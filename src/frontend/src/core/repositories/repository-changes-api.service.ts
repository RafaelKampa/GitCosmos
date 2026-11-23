import { ApiBaseService } from '../api/api-base-service';
import type { RepositoryChanges, DiffSelection, FileDiff } from './models/repository-changes.model';

export class RepositoryChangesApiService extends ApiBaseService {
  getChanges(workspaceId: number, repositoryId: number, signal?: AbortSignal) {
    const query = new URLSearchParams({ workspaceId: String(workspaceId), repositoryId: String(repositoryId) });
    return this.get<RepositoryChanges>((workspaceId === 0 ? '/api/RepositoryCatalog/GetChanges?' : '/api/RepositoryChanges/GetChanges?') + query, signal, null);
  }
  getDiff(workspaceId: number, repositoryId: number, selection: DiffSelection, signal?: AbortSignal) {
    const query = new URLSearchParams({ workspaceId: String(workspaceId), repositoryId: String(repositoryId), ...selection });
    return this.get<FileDiff>((workspaceId === 0 ? '/api/RepositoryCatalog/GetDiff?' : '/api/RepositoryChanges/GetDiff?') + query, signal, null);
  }
}
