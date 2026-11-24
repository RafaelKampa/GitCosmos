import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import { createRouter, createMemoryHistory } from 'vue-router';
import ExplorerRepositoryNode from './ExplorerRepositoryNode.vue';
import ExplorerSidebar from './ExplorerSidebar.vue';
import { useExplorerPreferences } from '../../core/preferences/explorer-preferences.store';
import { useExplorerStore } from '../../core/catalog/explorer.store';
import { useLanguageStore } from '../../core/i18n/language.store';
import RepositoryBranches from '../../features/repositories/repository-branches/RepositoryBranches.vue';
let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => view?.unmount());

it('expands without navigating, offers resource groups and preserves contextual navigation', async () => {
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: { template: '<div />' } }, { path: '/workspaces/1/repositories/2', component: { template: '<div />' } }] });
  await router.push('/'); await router.isReady();
  view = mount(ExplorerRepositoryNode, { props: { repositoryId: 2, workspaceId: 1, name: 'Repo', to: '/workspaces/1/repositories/2', active: false }, global: { plugins: [router], stubs: { RepositoryBranches: true } } });
  expect(view.find('.repository-tree-sections').exists()).toBe(false);
  await view.get('.repository-toggle').trigger('click');
  expect(router.currentRoute.value.path).toBe('/');
  expect(view.get('.repository-toggle').attributes('aria-expanded')).toBe('true');
  expect(view.get('.repository-toggle').attributes('aria-controls')).toBe(view.get('.repository-tree-sections').attributes('id'));
  expect(view.findAll('summary').map(item => item.text())).toEqual(['⑂Branches', '☁Remotes', '◇Tags', '▣Stashes', '▤Submódulos']);
  expect(view.findAll('details')).toHaveLength(5);
  expect(view.findComponent(RepositoryBranches).exists()).toBe(false);
  const group = view.get('details'); (group.element as HTMLDetailsElement).open = true; await group.trigger('toggle');
  expect(view.getComponent(RepositoryBranches).props()).toMatchObject({ repositoryId: 2, workspaceId: 1, remoteMode: false });
  (group.element as HTMLDetailsElement).open = false; await group.trigger('toggle');
  expect(view.findComponent(RepositoryBranches).exists()).toBe(false);
  const remotes = view.findAll('details')[1]!; (remotes.element as HTMLDetailsElement).open = true; await remotes.trigger('toggle');
  expect(view.getComponent(RepositoryBranches).props()).toMatchObject({ repositoryId: 2, workspaceId: 1, remoteMode: true });
  (remotes.element as HTMLDetailsElement).open = false; await remotes.trigger('toggle');
  expect(view.text()).toContain('próximos incrementos');
  await view.findAll('.repository-section-link')[1]!.trigger('click'); await flushPromises();
  expect(useExplorerPreferences().repositoryTabs[2]).toBe('history'); expect(router.currentRoute.value.path).toBe('/workspaces/1/repositories/2');
  await view.findAll('.repository-section-link')[0]!.trigger('click'); await flushPromises();
  expect(useExplorerPreferences().repositoryTabs[2]).toBe('changes');
  await view.setProps({ active: true }); expect(view.get('.repository-tree-link').attributes('aria-current')).toBe('page');
  expect(view.get('.repository-tree-heading').classes()).toContain('explorer-active');
  useLanguageStore().setLanguage('en'); await flushPromises(); expect(view.text()).toContain('Local changes'); expect(view.text()).toContain('Submodules');
  await view.get('.repository-toggle').trigger('click'); expect(view.find('.repository-tree-sections').exists()).toBe(false);
  expect(view.get('.repository-tree-link').attributes('href')).toBe('/workspaces/1/repositories/2');
});

it('keeps independent repository expansion in each workspace and for ungrouped entries', async () => {
  const explorer = useExplorerStore();
  explorer.index = { workspaces: [{ id: 1, name: 'One' }, { id: 2, name: 'Two' }], repositories: [
    { id: 3, name: 'Shared', localPath: 'C:/repo', workspaceIds: [1, 2], status: null, state: 'unknown', error: null },
    { id: 4, name: 'Solo', localPath: 'C:/solo', workspaceIds: [], status: null, state: 'unknown', error: null },
  ] };
  const refresh = vi.spyOn(explorer, 'refresh');
  view = mount(ExplorerSidebar, { props: { activeRepositoryId: 4 }, global: { stubs: { RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } } } });
  const nodes = view.findAllComponents(ExplorerRepositoryNode);
  expect(nodes[0]!.props('workspaceId')).toBe(1); expect(nodes[2]!.props('workspaceId')).toBe(0);
  await nodes[0]!.get('.repository-toggle').trigger('click');
  expect(nodes[1]!.find('.repository-tree-sections').exists()).toBe(false);
  await nodes[2]!.get('.repository-toggle').trigger('click');
  expect(nodes[2]!.get('.repository-tree-link').attributes('href')).toBe('/repositories/4');
  expect(nodes[2]!.get('.repository-tree-link').attributes('aria-current')).toBe('page');
  const workspace = view.get('.explorer-workspace');
  await workspace.get('.tree-toggle').trigger('click'); await workspace.get('.tree-toggle').trigger('click');
  expect(view.findAllComponents(ExplorerRepositoryNode)[0]!.get('.repository-toggle').attributes('aria-expanded')).toBe('true');
  expect(refresh).not.toHaveBeenCalled();
});
