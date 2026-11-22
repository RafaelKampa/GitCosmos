import { ApiBaseService } from '../api/api-base-service';
import type { CreateWorkspaceRequest } from './models/requests/create-workspace-request.model';
import type { WorkspaceResponse } from './models/responses/workspace-response.model';
import type { PagedResult } from '../../shared/models/pagination.model';

export class WorkspaceApiService extends ApiBaseService {
  getWorkspaces(page: number, signal?: AbortSignal) {
    const query = new URLSearchParams({ page: String(page), pageSize: '10' });
    return this.get<PagedResult<WorkspaceResponse>>('/api/Workspace/GetAllWorkspaces?' + query, signal);
  }

  createWorkspace(request: CreateWorkspaceRequest, signal?: AbortSignal) {
    return this.post<WorkspaceResponse>('/api/Workspace/CreateWorkspace', request, signal);
  }
}

