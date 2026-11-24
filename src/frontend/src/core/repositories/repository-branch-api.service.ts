import { ApiBaseService } from '../api/api-base-service';
import type { LocalGitBranches, RepositoryBranchPlan, RepositoryBranchRequest, BranchResources } from './models/repository-branch.model';

export class RepositoryBranchApiService extends ApiBaseService {
  getResources(repositoryId: number, workspaceId: number, signal?: AbortSignal) { return this.get<BranchResources>(`/api/RepositoryBranch/GetResources?repositoryId=${repositoryId}&workspaceId=${workspaceId}`, signal); }
  getBranches(repositoryId: number, workspaceId: number, signal?: AbortSignal) {
    return this.get<LocalGitBranches>(`/api/RepositoryBranch/GetBranches?repositoryId=${repositoryId}&workspaceId=${workspaceId}`, signal);
  }
  prepare(request: RepositoryBranchRequest, signal?: AbortSignal) { return this.post<RepositoryBranchPlan>('/api/RepositoryBranch/Prepare', request, signal); }
  execute(request: RepositoryBranchRequest, token: string, signal?: AbortSignal) { return this.post<string>('/api/RepositoryBranch/Execute', { request, token }, signal, null); }
}
