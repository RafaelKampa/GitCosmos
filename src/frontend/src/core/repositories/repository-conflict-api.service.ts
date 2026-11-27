import { ApiBaseService } from '../api/api-base-service';
import type { ConflictFile, RepositoryConflict, ResolveConflictRequest, ConflictResolutionPlan } from './models/repository-conflict.model';
export class RepositoryConflictApiService extends ApiBaseService {
  read(file: ConflictFile, signal?: AbortSignal) { return this.get<RepositoryConflict>('/api/RepositoryConflict/GetConflict?' + new URLSearchParams({ repositoryId: String(file.repositoryId), workspaceId: String(file.workspaceId), path: file.path }), signal); }
  prepare(request: ResolveConflictRequest, signal?: AbortSignal) { return this.post<ConflictResolutionPlan>('/api/RepositoryConflict/Prepare', request, signal); }
  resolve(request: ResolveConflictRequest, signal?: AbortSignal) { return this.post<boolean>('/api/RepositoryConflict/Resolve', request, signal); }
  external(file: ConflictFile, signal?: AbortSignal) { return this.post<boolean>('/api/RepositoryConflict/External', file, signal); }
}
