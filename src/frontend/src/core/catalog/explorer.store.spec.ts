import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useExplorerStore } from './explorer.store';
import { CatalogApiService } from './catalog-api.service';
import { ApiError } from '../api/api-error';
import type { CustomResponse } from '../../shared/models/custom-response.model';
import type { CatalogSnapshot } from './catalog.model';
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => vi.unstubAllGlobals());
const empty: CustomResponse<CatalogSnapshot> = { success: true, result: { workspaces: [], repositories: [] }, messages: [], errors: [] };
it.each([{workspaces:null,repositories:[]},{workspaces:[],repositories:null}])('rejects incomplete catalog collections: %s', async result => {
  vi.spyOn(CatalogApiService.prototype,'getExplorer').mockResolvedValue({ ...empty, result: result as unknown as CatalogSnapshot });
  const store=useExplorerStore(); await store.refresh(); expect(store.error).toContain('Não foi possível');
});
it.each([new ApiError('Localized'), new Error('private'), null])('reports failures and supports retry: %s', async cause => {
  const api = vi.spyOn(CatalogApiService.prototype, 'getExplorer');
  if (cause) api.mockRejectedValueOnce(cause); else api.mockResolvedValueOnce({ ...empty, result: null });
  api.mockResolvedValue(empty); const store = useExplorerStore(); await store.refresh();
  expect(store.error).toBe(cause instanceof ApiError ? cause.message : 'Não foi possível carregar o catálogo de repositórios.');
  await store.refresh(); expect(store.error).toBe(''); expect(store.loading).toBe(false);
});
it.each([true, false])('cancels and ignores stale responses: %s', async success => {
  let resolve!: (value: typeof empty) => void; let reject!: (error: Error) => void;
  const pending = new Promise<typeof empty>((ok, fail) => { resolve = ok; reject = fail; });
  const api = vi.spyOn(CatalogApiService.prototype, 'getExplorer').mockReturnValueOnce(pending).mockResolvedValue(empty);
  const store = useExplorerStore(); const first = store.refresh(); await store.refresh();
  expect(api.mock.calls[0]![0]!.aborted).toBe(true);
  if (success) resolve(empty); else reject(new Error('cancelled')); await first;
  store.cancel(); expect(api.mock.calls[1]![0]!.aborted).toBe(true); expect(store.error).toBe('');
});
