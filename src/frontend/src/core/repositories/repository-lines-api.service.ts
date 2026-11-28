import { ApiBaseService } from '../api/api-base-service';
import type { LinesFile, LinesSnapshot, PrepareLines, LinesPlan } from './models/repository-lines.model';
export class RepositoryLinesApiService extends ApiBaseService {
  read(file: LinesFile, signal?: AbortSignal) { return this.get<LinesSnapshot>('/api/RepositoryLines/GetSelection?' + new URLSearchParams({ repositoryId: String(file.repositoryId), workspaceId: String(file.workspaceId), path: file.path, area: file.area }), signal); }
  prepare(request: PrepareLines, signal?: AbortSignal) { return this.post<LinesPlan>('/api/RepositoryLines/Prepare', request, signal); }
  execute(request: PrepareLines, token: string, signal?: AbortSignal) { return this.post<boolean>('/api/RepositoryLines/Execute', { request, token }, signal); }
}
