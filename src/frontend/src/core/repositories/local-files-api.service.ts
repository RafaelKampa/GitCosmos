import { ApiBaseService } from '../api/api-base-service';
import type { LocalFilesRequest, LocalFilesView, LocalFilesPlan } from './models/local-files.model';
export class LocalFilesApiService extends ApiBaseService {
  read(request: LocalFilesRequest, signal?: AbortSignal) { return this.post<LocalFilesView>('/api/LocalFiles/Read', request, signal); }
  prepare(request: LocalFilesRequest, signal?: AbortSignal) { return this.post<LocalFilesPlan>('/api/LocalFiles/Prepare', request, signal); }
  execute(request: LocalFilesRequest, signal?: AbortSignal) { return this.post<boolean>('/api/LocalFiles/Execute', request, signal, 125000); }
  desktop(request: LocalFilesRequest, signal?: AbortSignal) { return this.post<boolean>('/api/LocalFiles/Desktop', request, signal); }
}
