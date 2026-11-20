import type { CustomResponse, ErrorResponse } from '../../shared/models/custom-response.model';
import type { TranslationKey } from '../i18n/language.store';
import { ApiError } from './api-error';

export class ApiBaseService {
  constructor(private readonly language: () => string, private readonly translate: (key: TranslationKey) => string) {}

  async get<T>(path: string, signal?: AbortSignal): Promise<CustomResponse<T>> {
    const timeout = AbortSignal.timeout(30_000);
    try {
      const response = await fetch(path, {
        headers: { 'Accept-Language': this.language() },
        signal: signal ? AbortSignal.any([signal, timeout]) : timeout,
      });
      const body = await response.json() as CustomResponse<T> & Partial<ErrorResponse>;
      if (!response.ok || !body.success)
        throw new ApiError(body.errors?.join(' ') || this.translate('networkError'), response.status, body.traceId);
      return body;
    } catch (error: unknown) {
      if (error instanceof ApiError) throw error;
      if (timeout.aborted) throw new ApiError(this.translate('timeout'));
      if (signal?.aborted) throw new ApiError(this.translate('cancelled'));
      throw new ApiError(this.translate('networkError'));
    }
  }
}
