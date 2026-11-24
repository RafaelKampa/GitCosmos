import { ApiBaseService } from '../api/api-base-service';
import type { RepositoryEditRequest, RepositoryEditPlan, RepositoryEditResult } from './models/repository-edit.model';
export class RepositoryEditApiService extends ApiBaseService {
  prepare(request: RepositoryEditRequest, signal?: AbortSignal) { return this.post<RepositoryEditPlan>('/api/RepositoryEdit/Prepare', request, signal); }
  execute(request: RepositoryEditRequest, token: string, signal?: AbortSignal) { return this.post<RepositoryEditResult>('/api/RepositoryEdit/Execute', { request, token }, signal, null); }
}
