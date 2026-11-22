import { ApiBaseService } from '../api/api-base-service';
import type { AddWorkspaceRepositoryRequest } from './models/requests/add-workspace-repository-request.model';
import type { WorkspaceRepositoryResponse } from './models/responses/workspace-repository-response.model';
import type { RepositoryImportResponse } from './models/responses/repository-import-response.model';

export class WorkspaceRepositoryApiService extends ApiBaseService {
  getRepositories(workspaceId: number, signal?: AbortSignal) {
    const query = new URLSearchParams({ workspaceId: String(workspaceId) });
    return this.get<readonly WorkspaceRepositoryResponse[]>('/api/WorkspaceRepository/GetRepositories?' + query, signal);
  }
  addRepository(request: AddWorkspaceRepositoryRequest, signal?: AbortSignal) {
    return this.post<WorkspaceRepositoryResponse>('/api/WorkspaceRepository/AddRepository', request, signal);
  }
  removeRepository(workspaceId: number, repositoryId: number, signal?: AbortSignal) {
    const query = new URLSearchParams({ workspaceId: String(workspaceId), repositoryId: String(repositoryId) });
    return this.delete<number>('/api/WorkspaceRepository/RemoveRepository?' + query, signal);
  }
  importRepositories(request: AddWorkspaceRepositoryRequest, signal?: AbortSignal) {
    return this.post<RepositoryImportResponse>('/api/RepositoryImport/ImportRepositories', request, signal, null);
  }
}
