import { defineStore } from 'pinia';
import { ref } from 'vue';
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
  async function refresh() {
    request?.abort(); const current = new AbortController(); request = current;
    loading.value = true; error.value = '';
    try {
      const response = await api.getExplorer(current.signal);
      if (!current.signal.aborted) {
        if (!response.result || !Array.isArray(response.result.workspaces) || !Array.isArray(response.result.repositories)) throw new ApiError(i18n.t('explorerError'));
        index.value = response.result;
      }
    } catch (cause: unknown) { if (!current.signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('explorerError'); }
    finally { if (!current.signal.aborted) loading.value = false; }
  }
  function cancel() { request?.abort(); }
  return { index, loading, error, refresh, cancel };
});
