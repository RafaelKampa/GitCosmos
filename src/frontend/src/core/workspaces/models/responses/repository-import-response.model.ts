export interface RepositoryImportItem {
  localPath: string;
  status: 'added' | 'duplicate' | 'ignored' | 'failed';
  message: string;
}
export interface RepositoryImportResponse { items: readonly RepositoryImportItem[]; }
