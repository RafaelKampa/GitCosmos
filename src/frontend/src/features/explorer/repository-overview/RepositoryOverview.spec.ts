import { afterEach, beforeEach, expect, it } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import RepositoryOverview from './RepositoryOverview.vue';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';
import type { CatalogRepository } from '../../../core/catalog/catalog.model';
let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); }); afterEach(() => wrapper?.unmount());
const repositories: CatalogRepository[] = [
  { id: 1, name: 'Alpha', localPath: 'C:/Alpha', workspaceIds: [1], state: 'clean', error: null, status: { branch: 'main', isDetached: false, hasCommits: true, changedFiles: 0, modified: 0, added: 0, deleted: 0, renamed: 0, untracked: 0, conflicted: 0, staged: 0, unstaged: 0, ahead: 2, behind: 0 } },
  { id: 2, name: 'Beta', localPath: 'C:/Beta', workspaceIds: [], state: 'modified', error: null, status: null },
  { id: 3, name: 'Gone', localPath: 'C:/Gone', workspaceIds: [], state: 'unavailable', error: 'Pasta movida', status: null },
];
function render() { wrapper = mount(RepositoryOverview, { props: { repositories, scopeKey: 'all', loading: false }, global: { stubs: { RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } } } }); return wrapper; }
it('keeps selection independent from navigation and applies select-all only to visible rows', async () => {
  const page = render(); const preferences = useExplorerPreferences();
  expect(page.get('.repository-open-link').attributes('href')).toBe('/repositories/1');
  await page.get('.repository-entry input').setValue(true); expect(preferences.selected.all).toEqual([1]);
  await page.get('input[type="search"]').setValue('Beta'); await page.get('.bulk-toolbar input').setValue(true);
  expect(preferences.selected.all).toEqual([1, 2]); await page.get('.bulk-toolbar input').setValue(false); expect(preferences.selected.all).toEqual([1]);
  await page.get('input[type="search"]').setValue(''); expect(page.findAll('.repository-entry')).toHaveLength(3);
  await page.setProps({ workspaceId: 5 }); expect(page.get('.repository-open-link').attributes('href')).toBe('/workspaces/5/repositories/1');
});
it('combines search, status, favorites and sorting across list/cards without losing selection', async () => {
  const page = render(); const preferences = useExplorerPreferences();
  await page.get('.favorite-action').trigger('click'); await page.get('.repository-filters > .icon-action').trigger('click');
  expect(page.findAll('.repository-entry')).toHaveLength(1);
  await page.get('.favorite-action').trigger('click'); expect(page.text()).toContain('Nenhum repositório corresponde');
  await page.get('.empty-panel button').trigger('click'); expect(page.findAll('.repository-entry')).toHaveLength(3);
  await page.get('select[aria-label="Filtrar por estado"]').setValue('modified'); expect(page.findAll('.repository-entry')).toHaveLength(1);
  await page.get('select[aria-label="Filtrar por estado"]').setValue('all'); await page.get('select[aria-label="Ordenar por nome"]').setValue('descending');
  expect(page.get('.repository-open-link strong').text()).toBe('Gone');
  await page.get('.view-toggle button:last-child').trigger('click'); expect(preferences.view).toBe('cards');
  await page.get('.view-toggle button:first-child').trigger('click'); expect(preferences.view).toBe('list');
  expect(page.text()).toContain('Pasta movida');
});
it('renders unknown, detached, conflicting, tracking and loading states honestly', async () => {
  const page = render();
  await page.setProps({ loading: true }); expect(page.get('.metric-tile dd').text()).toBe('—');
  expect(page.get('[role="status"]').text()).toContain('Consultando');
  await page.setProps({ loading: false, repositories: [ { ...repositories[0]!, state: 'conflicted', status: { ...repositories[0]!.status!, isDetached: true, behind: 3 } }, { ...repositories[1]!, state: 'unknown' } ] });
  expect(page.text()).toContain('HEAD destacado'); expect(page.text()).toContain('Não consultado');
  expect(page.findAll('.metric-tile dd').map(item => item.text())).toEqual(['2','1','1','1']);
});
