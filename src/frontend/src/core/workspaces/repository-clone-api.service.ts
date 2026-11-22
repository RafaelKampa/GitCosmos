import { ApiBaseService } from '../api/api-base-service';
import type { CloneWorkspaceRepositoryRequest, PreviewRepositoryCloneRequest, RepositoryClonePlan } from './models/requests/repository-clone.model';
import type { WorkspaceRepositoryResponse } from './models/responses/workspace-repository-response.model';

export class RepositoryCloneApiService extends ApiBaseService {
  preview(request: PreviewRepositoryCloneRequest, signal?: AbortSignal) {
    return this.post<RepositoryClonePlan>('/api/RepositoryClone/Preview', request, signal);
  }
  clone(request: CloneWorkspaceRepositoryRequest, signal?: AbortSignal) {
    return this.post<WorkspaceRepositoryResponse>('/api/RepositoryClone/CloneRepository', request, signal, null);
  }
}
