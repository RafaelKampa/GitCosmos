import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import { createRouter, createMemoryHistory } from 'vue-router';
import RepositoryBranches from './RepositoryBranches.vue';
import BranchContextMenu from './BranchContextMenu.vue';
import BranchActionDialog from './BranchActionDialog.vue';
import { RepositoryBranchApiService } from '../../../core/repositories/repository-branch-api.service';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { LocalGitBranches, BranchResources } from '../../../core/repositories/models/repository-branch.model';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
const data: LocalGitBranches = { currentBranch: 'main', isDetached: false, hasCommits: true, branches: [
  { name: 'main', sha: 'a'.repeat(40), upstream: 'refs/remotes/origin/main', isCurrent: true, isCheckedOut: true },
  { name: 'feature/other', sha: 'b'.repeat(40), upstream: null, isCurrent: false, isCheckedOut: false },
] };
const resources: BranchResources = { currentBranch: 'main', operationState: '', remotes: ['origin'], branches: [{ name: 'origin/feature', remote: 'origin', branch: 'feature', sha: 'b'.repeat(40) }] };
const ok = <T,>(result: T | null): CustomResponse<T> => ({ success: true, result, messages: [], errors: [] });
function deferred<T>() { let resolve!: (value: T) => void; let reject!: (error: Error) => void; const promise = new Promise<T>((ok, fail) => { resolve = ok; reject = fail; }); return { promise, resolve, reject }; }
let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { view?.unmount(); view = undefined; vi.unstubAllGlobals(); });
function mock() { return { list: vi.spyOn(RepositoryBranchApiService.prototype, 'getBranches').mockResolvedValue(ok(data)), resources: vi.spyOn(RepositoryBranchApiService.prototype, 'getResources').mockResolvedValue(ok(resources)) }; }
function render(remoteMode = false) {
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { template: '<div />' } }] });
  view = mount(RepositoryBranches, { props: { repositoryId: 2, workspaceId: 1, remoteMode }, global: { plugins: [router], stubs: { Teleport: true, BranchActionDialog: true } } }); return view;
}
it('searches names and navigates to a branch SHA without changing filters or checking out', async () => {
  const api = mock(); const page = render(); await flushPromises();
  expect(page.find('form').exists()).toBe(false); expect(page.find('.branch-actions-toggle').exists()).toBe(false);
  useExplorerPreferences().historyFilters[2] = { branch: 'main', search: 'saved' };
  await page.get('.branch-search input').setValue(' OTHER '); expect(page.findAll('.branch-select')).toHaveLength(1);
  await page.get('.branch-select').trigger('click'); await flushPromises();
  expect(page.vm.$router.currentRoute.value.path).toBe('/workspaces/1/repositories/2');
  expect(useExplorerPreferences().historyNavigation[2]).toEqual({ branch: 'feature/other', sha: 'b'.repeat(40), revision: 1 });
  expect(useExplorerPreferences().historyFilters[2]).toEqual({ branch: 'main', search: 'saved' });
  await page.get('.branch-select').trigger('click'); expect(useExplorerPreferences().historyNavigation[2]!.revision).toBe(2);
  await page.get('.branch-search input').setValue('missing'); expect(page.text()).toContain('Nenhuma branch');
  await page.get('.branch-search input').setValue(''); expect(page.findAll('.branch-select')).toHaveLength(2);
  await page.setProps({ workspaceId: 0 }); await flushPromises(); await page.get('.branch-select').trigger('click'); await flushPromises(); expect(page.vm.$router.currentRoute.value.path).toBe('/repositories/2');
  expect(api.resources).not.toHaveBeenCalled();
});
it('opens the contextual action for its own branch, handles busy/close and refreshes after changes or uncertain results', async () => {
  const api = mock(); const page = render(); await flushPromises();
  await page.findAll('.branch-options')[1]!.trigger('click'); await flushPromises();
  expect(page.findAll('.branch-select').map(button => button.text())).toEqual([expect.stringContaining('main'), expect.stringContaining('feature/other')]);
  await page.get('[data-operation="rename"]').trigger('click'); await flushPromises();
  const dialog = page.getComponent(BranchActionDialog); expect(dialog.props()).toMatchObject({ branch: { name: 'feature/other' }, operation: 'rename', resources });
  dialog.vm.$emit('busy', true); await flushPromises(); expect(page.get('.branch-select').attributes('disabled')).toBeDefined();
  const calls = api.resources.mock.calls.length; page.getComponent(BranchContextMenu).vm.$emit('select', 'copy'); await flushPromises(); expect(api.resources).toHaveBeenCalledTimes(calls);
  dialog.vm.$emit('close'); await flushPromises(); expect(page.findComponent(BranchActionDialog).exists()).toBe(false);
  page.findAllComponents(BranchContextMenu)[1]!.vm.$emit('select', 'merge'); await flushPromises();
  page.getComponent(BranchActionDialog).vm.$emit('uncertain'); await flushPromises(); expect(useExplorerPreferences().repositoryVersions[2]).toBe(1);
  page.getComponent(BranchActionDialog).vm.$emit('changed', 'Completed'); await flushPromises(); expect(page.text()).toContain('Completed'); expect(useExplorerPreferences().repositoryVersions[2]).toBe(2);
  expect(api.list.mock.calls.length).toBeGreaterThan(1);
});
it('keeps tracked local branches visible after loading resources and refreshing the list', async () => {
  const api = mock(); const page = render(); await flushPromises();
  page.getComponent(BranchContextMenu).vm.$emit('select', 'push'); await flushPromises();
  expect(page.findAllComponents(BranchContextMenu).map(menu => menu.props('branch').name)).toEqual(['main', 'feature/other']);
  expect(page.getComponent(BranchActionDialog).props('branch')).toMatchObject({ name: 'main', remote: 'origin', sourceBranch: 'main' });
  page.getComponent(BranchActionDialog).vm.$emit('close'); await flushPromises();
  await page.get('.branch-refresh').trigger('click'); await flushPromises();
  expect(api.list).toHaveBeenCalledTimes(2);
  expect(page.findAll('.branch-select')).toHaveLength(2);
  await page.get('.branch-search input').setValue('main');
  expect(page.get('.branch-select').text()).toContain('main');
});
it.each([false, true])('copies the chosen name or reports clipboard errors (failure=%s)', async failure => {
  mock(); const writeText = failure ? vi.fn().mockRejectedValue(new Error('denied')) : vi.fn().mockResolvedValue(undefined);
  vi.stubGlobal('navigator', { clipboard: { writeText }, userAgent: '' });
  const page = render(); await flushPromises(); page.findAllComponents(BranchContextMenu)[1]!.vm.$emit('select', 'copy'); await flushPromises();
  expect(writeText).toHaveBeenCalledWith('feature/other'); expect(page.text()).toContain(useLanguageStore().t(failure ? 'branchCopyError' : 'branchCopied'));
});
it('uses the selected local branch tracking remote and server branch as push defaults', async () => {
  const api = mock(); api.list.mockResolvedValue(ok({ ...data, branches: [{ ...data.branches[1]!, upstream: 'refs/remotes/origin/server-feature' }] }));
  api.resources.mockResolvedValue(ok({ ...resources, remotes: ['backup', 'origin'] }));
  const page = render(); await flushPromises(); page.getComponent(BranchContextMenu).vm.$emit('select', 'push'); await flushPromises();
  expect(page.getComponent(BranchActionDialog).props('branch')).toMatchObject({ name: 'feature/other', remote: 'origin', sourceBranch: 'server-feature' });
});
it('propagates language through the real HTTP service for branch and remote resource lookups', async () => {
  const fetchMock = vi.fn(async (url: string) => new Response(JSON.stringify(ok(url.includes('/GetResources') ? resources : data))));
  vi.stubGlobal('fetch', fetchMock); useLanguageStore().setLanguage('es');
  const page = render(); await flushPromises();
  expect(fetchMock).toHaveBeenLastCalledWith('/api/RepositoryBranch/GetBranches?repositoryId=2&workspaceId=1', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  page.findAllComponents(BranchContextMenu)[1]!.vm.$emit('select', 'push'); await flushPromises();
  expect(fetchMock).toHaveBeenLastCalledWith('/api/RepositoryBranch/GetResources?repositoryId=2&workspaceId=1', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  expect(page.getComponent(BranchActionDialog).props('operation')).toBe('push');
  useLanguageStore().setLanguage('en'); await flushPromises();
  expect(fetchMock).toHaveBeenLastCalledWith('/api/RepositoryBranch/GetBranches?repositoryId=2&workspaceId=1', expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
});
it('groups remote branches and offers fetch and recovery for the repository', async () => {
  const api = mock(); api.resources.mockResolvedValue(ok({ ...resources, operationState: 'rebase', remotes: ['origin', 'backup'], branches: [...resources.branches, { name: 'backup/main', remote: 'backup', branch: 'main', sha: 'c'.repeat(40) }] }));
  const page = render(true); await flushPromises(); expect(api.list).not.toHaveBeenCalled();
  const groups = page.findAll('.remote-branch-group');
  expect(groups.map(group => group.get('h4').text())).toEqual(['origin', 'backup']);
  expect(groups[0]!.findAll('.branch-select').map(button => button.text())).toEqual([expect.stringContaining('feature')]);
  expect(groups[1]!.findAll('.branch-select').map(button => button.text())).toEqual([expect.stringContaining('main')]);
  await page.get('.branch-fetch').trigger('click'); await flushPromises(); expect(page.getComponent(BranchActionDialog).props('operation')).toBe('fetch');
  page.getComponent(BranchActionDialog).vm.$emit('close'); await flushPromises();
  await page.findAll('.branch-recovery button')[0]!.trigger('click'); await flushPromises(); expect(page.getComponent(BranchActionDialog).props('operation')).toBe('continue');
  page.getComponent(BranchActionDialog).vm.$emit('close'); await flushPromises();
  await page.findAll('.branch-recovery button')[1]!.trigger('click'); await flushPromises(); expect(page.getComponent(BranchActionDialog).props('operation')).toBe('abort');
  page.getComponent(BranchActionDialog).vm.$emit('close'); await flushPromises();
  api.resources.mockResolvedValue(ok({ ...resources, branches: [], currentBranch: '' })); await page.get('.branch-refresh').trigger('click'); await flushPromises();
  await page.get('.branch-fetch').trigger('click'); await flushPromises(); expect(page.getComponent(BranchActionDialog).props('branch')).toMatchObject({ name: 'origin', remote: 'origin' });
});
it.each([new ApiError('Localized'), new Error('private'), null])('shows read and resource failures without opening an action: %s', async failure => {
  const api = mock(); if (failure) api.list.mockRejectedValueOnce(failure); else api.list.mockResolvedValueOnce(ok<LocalGitBranches>(null));
  const page = render(); await flushPromises(); expect(page.get('[role="alert"]').text()).not.toContain('private');
  await page.get('.branch-refresh').trigger('click'); await flushPromises();
  if (failure) api.resources.mockRejectedValueOnce(failure); else api.resources.mockResolvedValueOnce(ok<BranchResources>(null));
  page.getComponent(BranchContextMenu).vm.$emit('select', 'create'); await flushPromises(); expect(page.findComponent(BranchActionDialog).exists()).toBe(false); expect(page.get('[role="alert"]').text()).toBeTruthy();
});
it('handles unborn, detached and other-worktree states and language changes', async () => {
  const api = mock(); api.list.mockResolvedValueOnce(ok({ ...data, hasCommits: false, branches: [] }));
  const page = render(); await flushPromises(); expect(page.text()).toContain('primeiro commit');
  api.list.mockResolvedValueOnce(ok({ ...data, isDetached: true, branches: [{ ...data.branches[1]!, isCheckedOut: true }] })); await page.get('.branch-refresh').trigger('click'); await flushPromises();
  expect(page.text()).toContain(useLanguageStore().t('gitDetached')); expect(page.text()).toContain('outra worktree');
  useLanguageStore().setLanguage('en'); await flushPromises(); expect(page.text()).toContain('Find branch');
});
it.each(['list', 'resources'].flatMap(stage => [false, true].map(failed => ({ stage, failed }))))('aborts stale $stage results when changing context (failed=$failed)', async ({ stage, failed }) => {
  const api = mock(); const pending = deferred<CustomResponse<LocalGitBranches> & CustomResponse<BranchResources>>();
  if (stage === 'list') api.list.mockReturnValueOnce(pending.promise); else api.resources.mockReturnValueOnce(pending.promise);
  const page = render(stage === 'resources'); await flushPromises(); await page.setProps({ repositoryId: 3, workspaceId: 0 }); await flushPromises();
  const signal = stage === 'list' ? api.list.mock.calls[0]![2]! : api.resources.mock.calls[0]![2]!; expect(signal.aborted).toBe(true);
  if (failed) pending.reject(new Error('late')); else pending.resolve(ok(stage === 'list' ? data : resources) as CustomResponse<LocalGitBranches> & CustomResponse<BranchResources>);
  await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false);
  page.unmount(); view = undefined;
});
