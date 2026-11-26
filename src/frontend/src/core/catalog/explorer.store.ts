import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { useLanguageStore } from '../i18n/language.store';
import { CatalogApiService } from './catalog-api.service';
import type { CatalogSnapshot } from './catalog.model';
import { ApiError } from '../api/api-error';

export const useExplorerStore = defineStore('explorer', () => {
  const i18n = useLanguageStore();
  const api = new CatalogApiService(() => i18n.language, i18n.t);
  const index = ref<CatalogSnapshot>({ workspaces: [], repositories: [] });
  const loading = ref(false); const error = ref('');
  let request: AbortController | undefined;
  let statusRequest: AbortController | undefined; let timer: ReturnType<typeof setInterval> | undefined;
  const refreshingStatus = ref(false);
  const changedRepositoryIds = computed(() => index.value.repositories.filter(repo => repo.status !== null && repo.status.changedFiles > 0).map(repo => repo.id));
  const changedWorkspaceIds = computed(() => [...new Set(index.value.repositories.filter(repo => changedRepositoryIds.value.includes(repo.id)).flatMap(repo => repo.workspaceIds))]);
  async function refresh() {
    request?.abort(); const current = new AbortController(); request = current;
    loading.value = true; error.value = '';
    try {
      const response = await api.getExplorer(current.signal);
      if (!current.signal.aborted) {
        if (!response.result || !Array.isArray(response.result.workspaces) || !Array.isArray(response.result.repositories)) throw new ApiError(i18n.t('explorerError'));
        index.value = { ...response.result, repositories: response.result.repositories.map(repo => {
          const previous = index.value.repositories.find(item => item.id === repo.id && item.localPath === repo.localPath);
          return previous && repo.state === 'unknown' ? { ...repo, status: previous.status, state: previous.state, error: previous.error } : repo;
        }) };
      }
    } catch (cause: unknown) { if (!current.signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('explorerError'); }
    finally { if (!current.signal.aborted) loading.value = false; }
  }
  async function refreshStatus() {
    if (refreshingStatus.value) return;
    const current = new AbortController(); statusRequest = current; refreshingStatus.value = true;
    try {
      const response = await api.getOverview(current.signal);
      if (!current.signal.aborted) {
        if (!response.result || !Array.isArray(response.result.workspaces) || !Array.isArray(response.result.repositories)) throw new ApiError(i18n.t('explorerError'));
        index.value = response.result; error.value = '';
      }
    } catch (cause: unknown) { if (!current.signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('explorerError'); }
    finally { if (statusRequest === current) refreshingStatus.value = false; }
  }
  function startAutoRefresh() {
    if (timer !== undefined) return;
    void refreshStatus(); timer = setInterval(refreshStatus, 60_000);
  }
  function cancel() {
    request?.abort(); statusRequest?.abort(); refreshingStatus.value = false;
    if (timer !== undefined) { clearInterval(timer); timer = undefined; }
  }
  return { index, loading, error, refreshingStatus, changedRepositoryIds, changedWorkspaceIds, refresh, refreshStatus, startAutoRefresh, cancel };
});
