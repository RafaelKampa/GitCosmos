import { ApiBaseService } from '../api/api-base-service';
import type { FolderSelectionResponse, SelectFolderRequest } from './models/folder-selection.model';

export class LocalFolderApiService extends ApiBaseService {
  selectFolder(request: SelectFolderRequest, signal?: AbortSignal) {
    // Folder selection is an explicit user interaction; it has no automatic HTTP timeout.
    return this.post<FolderSelectionResponse>('/api/LocalFolder/SelectFolder', request, signal, null);
  }
}
