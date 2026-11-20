import { ApiBaseService } from '../api/api-base-service';
import type { SystemStatusResponse } from './models/responses/system-status-response.model';

export class SystemApiService extends ApiBaseService {
  getStatus(signal?: AbortSignal) {
    return this.get<SystemStatusResponse>('/api/System/GetStatus', signal);
  }
}
