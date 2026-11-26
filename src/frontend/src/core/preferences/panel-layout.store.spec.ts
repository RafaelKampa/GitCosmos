import { beforeEach, afterEach, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { nextTick } from 'vue';
import { usePanelLayout } from './panel-layout.store';
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => vi.restoreAllMocks());
it('persists panel sizes independently of repository navigation', async () => {
  const store = usePanelLayout(); expect(store.sizes).toEqual({ changesWidth: 30, changesHeight: 0, unstaged: 50, historyGraph: 0, historyDetails: 0, commitTreeWidth: 30 });
  store.sizes = { changesWidth: 40, changesHeight: 800, unstaged: 60, historyGraph: 300, historyDetails: 500, commitTreeWidth: 45 }; await nextTick();
  setActivePinia(createPinia()); expect(usePanelLayout().sizes).toEqual(store.sizes);
});
it.each(['broken', 'null', '[]', '{"changesWidth":1e999}', '{"changesWidth":10,"unstaged":"60","historyGraph":999999,"changesHeight":-1,"historyDetails":null}'])('rejects invalid saved dimensions: %s', value => {
  localStorage.setItem('gitcosmos.panelSizes', value); expect(usePanelLayout().sizes.changesWidth).toBe(30); expect(usePanelLayout().sizes.historyGraph).toBe(0);
});
it('continues resizing when browser storage is blocked', async () => {
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('quota'); });
  const store = usePanelLayout(); store.sizes.changesWidth = 45; await nextTick(); expect(store.sizes.changesWidth).toBe(45);
});
