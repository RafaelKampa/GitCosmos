import type { RepositoryDashboardResponse } from '../workspaces/models/responses/workspace-dashboard-response.model';
export interface CatalogWorkspace { id: number; name: string; }
export interface CatalogRepository extends Omit<RepositoryDashboardResponse, 'state'> {
  workspaceIds: readonly number[];
  state: RepositoryDashboardResponse['state'] | 'unknown';
}
export interface CatalogSnapshot { workspaces: readonly CatalogWorkspace[]; repositories: readonly CatalogRepository[]; }
