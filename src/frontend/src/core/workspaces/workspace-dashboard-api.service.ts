import { ApiBaseService } from '../api/api-base-service';
import type { WorkspaceDashboardResponse } from './models/responses/workspace-dashboard-response.model';

export class WorkspaceDashboardApiService extends ApiBaseService {
  getDashboard(workspaceId: number, signal?: AbortSignal) {
    const query = new URLSearchParams({ workspaceId: String(workspaceId) });
    return this.get<WorkspaceDashboardResponse>('/api/WorkspaceDashboard/GetDashboard?' + query, signal, null);
  }
}
