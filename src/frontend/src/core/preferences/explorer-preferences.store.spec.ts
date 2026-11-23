import { beforeEach, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { nextTick } from 'vue';
import { useExplorerPreferences } from './explorer-preferences.store';
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
it('persists favorites, view and collapse while keeping scoped selection independent of filters', async () => {
  const store = useExplorerPreferences(); store.scope('all'); store.scope('workspace:1');
  store.toggleSelection('all', 1); store.toggleSelection('all', 2); store.toggleSelection('all', 1);
  expect(store.selected.all).toEqual([2]); expect(store.selected['workspace:1']).toEqual([]);
  store.selectVisible('all', [2, 3], true); expect(store.selected.all).toEqual([2, 3]);
  store.selectVisible('all', [3], false); expect(store.selected.all).toEqual([2]);
  store.toggleFavorite(2); store.toggleFavorite(3); store.toggleFavorite(3); store.view = 'cards'; store.collapsed = true; store.sidebarWidth = 320;
  await nextTick(); setActivePinia(createPinia());
  const restored = useExplorerPreferences(); expect(restored.favorites).toEqual([2]); expect(restored.view).toBe('cards'); expect(restored.collapsed).toBe(true); expect(restored.sidebarWidth).toBe(320);
});
it.each(['invalid json', '{}', '[1,"2",-1,0,3]'])('sanitizes persisted favorites: %s', value => {
  localStorage.setItem('gitcosmos.favorites', value); const store = useExplorerPreferences();
  expect(store.favorites).toEqual(value.startsWith('[') ? [1, 3] : []);
});
it.each([['1000',260],['invalid',260],['220',220],['380',380]])('bounds the saved Explorer width: %s', (value, expected) => {
  localStorage.setItem('gitcosmos.explorerWidth', String(value)); expect(useExplorerPreferences().sidebarWidth).toBe(expected);
});
