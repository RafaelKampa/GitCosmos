export interface CustomResponse<T> {
  readonly success: boolean;
  readonly errors: readonly string[];
  readonly messages: readonly string[];
  readonly result: T | null;
  readonly totalRows?: number | null;
}

export interface ErrorResponse {
  readonly success: false;
  readonly errors: readonly string[];
  readonly errorCode: string;
  readonly traceId: string;
}
