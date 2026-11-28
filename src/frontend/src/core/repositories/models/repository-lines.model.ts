import type { FileDiff, DiffArea } from './repository-changes.model';
export interface LinesFile { repositoryId: number; workspaceId: number; path: string; area: DiffArea; }
export interface LinesSnapshot { diff: FileDiff; token: string; canSelect: boolean; reason: string; }
export interface PrepareLines { file: LinesFile; lines: number[]; token: string; operation?: 'move' | 'discard'; exportPatch?: boolean; }
export interface LinesPlan { token: string; path: string; area: DiffArea; lineCount: number; result: string; removesEntry: boolean; operation?: 'move' | 'discard'; patch?: string; }
