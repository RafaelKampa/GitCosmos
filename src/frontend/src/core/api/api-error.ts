export class ApiError extends Error {
  constructor(message: string, readonly status?: number, readonly traceId?: string) {
    super(message);
    this.name = 'ApiError';
  }
}
