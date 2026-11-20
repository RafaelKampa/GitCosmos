export interface PaginationRequest { readonly page?: number; readonly pageSize?: number; }
export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalRows: number;
  readonly totalPages: number;
}
