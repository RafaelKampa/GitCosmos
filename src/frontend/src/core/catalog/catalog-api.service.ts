import { ApiBaseService } from '../api/api-base-service';
import type { CatalogSnapshot } from './catalog.model';
import type { RepositoryImportResponse } from '../workspaces/models/responses/repository-import-response.model';
import type { RepositoryClonePlan, PreviewRepositoryCloneRequest, CloneWorkspaceRepositoryRequest } from '../workspaces/models/requests/repository-clone.model';
import type { WorkspaceRepositoryResponse } from '../workspaces/models/responses/workspace-repository-response.model';

export class CatalogApiService extends ApiBaseService {
  getExplorer(signal?: AbortSignal) { return this.get<CatalogSnapshot>('/api/RepositoryCatalog/GetExplorer', signal); }
  getOverview(signal?: AbortSignal) { return this.get<CatalogSnapshot>('/api/RepositoryCatalog/GetOverview', signal, null); }
  addRepository(localPath: string, signal?: AbortSignal) { return this.post<RepositoryImportResponse>('/api/RepositoryCatalog/AddRepository', { localPath }, signal, null); }
  preview(request: PreviewRepositoryCloneRequest, signal?: AbortSignal) { return this.post<RepositoryClonePlan>('/api/RepositoryCatalog/Preview', request, signal); }
  clone(request: CloneWorkspaceRepositoryRequest, signal?: AbortSignal) { return this.post<WorkspaceRepositoryResponse>('/api/RepositoryCatalog/CloneRepository', request, signal, null); }
}
