import type { CustomResponse, ErrorResponse } from '../../shared/models/custom-response.model';
import type { TranslationKey } from '../i18n/language.store';
import { ApiError } from './api-error';

export class ApiBaseService {
  constructor(private readonly language: () => string, private readonly translate: (key: TranslationKey) => string) {}

  async get<T>(path: string, signal?: AbortSignal): Promise<CustomResponse<T>> {
    return this.request<T>(path, {}, signal);
  }

  async post<T>(path: string, data: unknown, signal?: AbortSignal): Promise<CustomResponse<T>> {
    return this.sendJson<T>('POST', path, data, signal);
  }

  async put<T>(path: string, data: unknown, signal?: AbortSignal): Promise<CustomResponse<T>> {
    return this.sendJson<T>('PUT', path, data, signal);
  }

  async delete<T>(path: string, signal?: AbortSignal): Promise<CustomResponse<T>> {
    return this.request<T>(path, { method: 'DELETE' }, signal);
  }

  private async sendJson<T>(method: 'POST' | 'PUT', path: string, data: unknown, signal?: AbortSignal): Promise<CustomResponse<T>> {
    return this.request<T>(path, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }, signal);
  }

  private async request<T>(path: string, init: RequestInit, signal?: AbortSignal): Promise<CustomResponse<T>> {
    const timeout = AbortSignal.timeout(30_000);
    try {
      const response = await fetch(path, {
        ...init,
        headers: { 'Accept-Language': this.language(), ...init.headers },
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
