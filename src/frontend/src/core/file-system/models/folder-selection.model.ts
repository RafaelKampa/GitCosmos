export interface SelectFolderRequest { readonly initialPath: string | null; }
export interface FolderSelectionResponse { readonly localPath: string | null; readonly cancelled: boolean; }
