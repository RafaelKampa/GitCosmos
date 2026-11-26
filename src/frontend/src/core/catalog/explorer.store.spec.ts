import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { nextTick } from 'vue';
import { useExplorerStore } from './explorer.store';
import { CatalogApiService } from './catalog-api.service';
import { ApiError } from '../api/api-error';
import type { CustomResponse } from '../../shared/models/custom-response.model';
import type { CatalogSnapshot } from './catalog.model';
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { useExplorerStore().cancel(); vi.useRealTimers(); vi.unstubAllGlobals(); });
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
const dirty: CatalogSnapshot = { workspaces: [{ id: 1, name: 'One' }, { id: 2, name: 'Two' }], repositories: [{ id: 1, name: 'Shared', localPath: 'C:/repo', workspaceIds: [1, 2], state: 'modified', error: null, status: { branch: 'main', isDetached: false, hasCommits: true, changedFiles: 1, modified: 1, added: 0, deleted: 0, renamed: 0, untracked: 0, conflicted: 0, staged: 0, unstaged: 1 } }] };
it('refreshes every 60 seconds once, supports manual refresh and stops on cancellation', async () => {
  vi.useFakeTimers(); const api = vi.spyOn(CatalogApiService.prototype, 'getOverview').mockResolvedValue({ ...empty, result: dirty });
  const store = useExplorerStore(); store.startAutoRefresh(); store.startAutoRefresh(); await nextTick();
  expect(api).toHaveBeenCalledTimes(1); expect(store.changedRepositoryIds).toEqual([1]); expect(store.changedWorkspaceIds).toEqual([1, 2]);
  await vi.advanceTimersByTimeAsync(59999); expect(api).toHaveBeenCalledTimes(1);
  await vi.advanceTimersByTimeAsync(1); expect(api).toHaveBeenCalledTimes(2);
  await store.refreshStatus(); expect(api).toHaveBeenCalledTimes(3);
  store.cancel(); await vi.advanceTimersByTimeAsync(120000); expect(api).toHaveBeenCalledTimes(3);
  store.startAutoRefresh(); await nextTick(); expect(api).toHaveBeenCalledTimes(4);
});
it.each([true, false])('does not overlap status requests or apply cancelled completion (success=%s)', async success => {
  let resolve!: (value: typeof empty) => void; let reject!: (error: Error) => void;
  const api = vi.spyOn(CatalogApiService.prototype, 'getOverview').mockReturnValueOnce(new Promise((ok, fail) => { resolve = ok; reject = fail; }));
  const store = useExplorerStore(); const pending = store.refreshStatus(); await store.refreshStatus(); expect(api).toHaveBeenCalledTimes(1);
  store.cancel(); expect(api.mock.calls[0]![0]!.aborted).toBe(true);
  if (success) resolve({ ...empty, result: dirty }); else reject(new Error('late')); await pending;
  expect(store.index.repositories).toEqual([]); expect(store.error).toBe(''); expect(store.refreshingStatus).toBe(false);
});
it.each([new ApiError('Localized'), new Error('private'), null, { workspaces: null, repositories: [] }, { workspaces: [], repositories: null }])('preserves last known status on failure and recovers: %s', async cause => {
  const api = vi.spyOn(CatalogApiService.prototype, 'getOverview');
  if (cause instanceof Error) api.mockRejectedValueOnce(cause); else api.mockResolvedValueOnce({ ...empty, result: cause as unknown as CatalogSnapshot });
  const store = useExplorerStore(); store.index = dirty; await store.refreshStatus(); expect(store.error).not.toContain('private'); expect(store.changedWorkspaceIds).toEqual([1, 2]);
  api.mockResolvedValue(empty); await store.refreshStatus(); expect(store.error).toBe(''); expect(store.changedRepositoryIds).toEqual([]); expect(store.changedWorkspaceIds).toEqual([]);
});
it('preserves known status through metadata reloads but never carries it to a different path', async () => {
  const api = vi.spyOn(CatalogApiService.prototype, 'getExplorer'); const store = useExplorerStore(); store.index = dirty;
  api.mockResolvedValue({ ...empty, result: { ...dirty, repositories: [{ ...dirty.repositories[0]!, state: 'unknown', status: null }] } });
  await store.refresh(); expect(store.index.repositories[0]!.status?.changedFiles).toBe(1);
  api.mockResolvedValue({ ...empty, result: { ...dirty, repositories: [{ ...dirty.repositories[0]!, localPath: 'C:/different', state: 'unknown', status: null }] } });
  await store.refresh(); expect(store.changedRepositoryIds).toEqual([]);
});
