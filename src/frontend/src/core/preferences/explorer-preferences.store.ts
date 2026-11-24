import { defineStore } from 'pinia';
import { ref, watch } from 'vue';

export interface RepositoryFilters { search: string; status: string; favoritesOnly: boolean; sort: string; }
function savedFavorites(): number[] {
  try {
    const value: unknown = JSON.parse(localStorage.getItem('gitcosmos.favorites') ?? '[]');
    return Array.isArray(value) ? value.filter((id): id is number => Number.isInteger(id) && id > 0) : [];
  } catch { return []; }
}
export const useExplorerPreferences = defineStore('explorer-preferences', () => {
  const savedCollapse = localStorage.getItem('gitcosmos.explorerCollapsed');
  const collapsed = ref(savedCollapse === null ? window.innerWidth <= 860 : savedCollapse === 'true');
  const view = ref<'list' | 'cards'>(localStorage.getItem('gitcosmos.repositoryView') === 'cards' ? 'cards' : 'list');
  const favorites = ref(savedFavorites());
  const operationsOpen = ref(false);
  const savedWidth = Number(localStorage.getItem('gitcosmos.explorerWidth'));
  const sidebarWidth = ref(savedWidth >= 220 && savedWidth <= 380 ? savedWidth : 260);
  const filters = ref<Record<string, RepositoryFilters>>({});
  const selected = ref<Record<string, number[]>>({});
  const repositoryTabs = ref<Record<number, string>>({});
  const expandedRepositories = ref<Record<string, boolean>>({});
  const repositoryVersions = ref<Record<number, number>>({});
  const historyNavigation = ref<Record<number, { branch: string; sha?: string; revision: number }>>({});
  const historyFilters = ref<Record<number, { branch: string; search: string }>>({});
  function scope(key: string) {
    filters.value[key] ??= { search: '', status: 'all', favoritesOnly: false, sort: 'name' };
    selected.value[key] ??= [];
    return filters.value[key];
  }
  function toggleFavorite(id: number) { favorites.value = favorites.value.includes(id) ? favorites.value.filter(value => value !== id) : [...favorites.value, id]; }
  function toggleSelection(key: string, id: number) {
    scope(key); const ids = selected.value[key]!;
    selected.value[key] = ids.includes(id) ? ids.filter(value => value !== id) : [...ids, id];
  }
  function selectVisible(key: string, ids: readonly number[], select: boolean) {
    scope(key);
    selected.value[key] = select ? [...new Set([...selected.value[key]!, ...ids])] : selected.value[key]!.filter(id => !ids.includes(id));
  }
  watch(collapsed, value => localStorage.setItem('gitcosmos.explorerCollapsed', String(value)));
  watch(view, value => localStorage.setItem('gitcosmos.repositoryView', value));
  watch(sidebarWidth, value => localStorage.setItem('gitcosmos.explorerWidth', String(value)));
  watch(favorites, value => localStorage.setItem('gitcosmos.favorites', JSON.stringify(value)), { deep: true });
  return { collapsed, view, favorites, operationsOpen, sidebarWidth, filters, selected, repositoryTabs, expandedRepositories, repositoryVersions, historyNavigation, historyFilters, scope, toggleFavorite, toggleSelection, selectVisible };
});
