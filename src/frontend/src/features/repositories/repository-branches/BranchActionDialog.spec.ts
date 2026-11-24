import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import Dialog from 'primevue/dialog';
import BranchActionDialog from './BranchActionDialog.vue';
import { RepositoryBranchApiService } from '../../../core/repositories/repository-branch-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { BranchItem, BranchOperation, BranchResources, RepositoryBranchPlan } from '../../../core/repositories/models/repository-branch.model';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
const branch: BranchItem = { name: 'feature', sha: 'a'.repeat(40), upstream: null, isCurrent: false, isCheckedOut: false, isRemote: false, remote: '', sourceBranch: 'feature' };
const remoteBranch: BranchItem = { ...branch, name: 'origin/feature', isRemote: true, remote: 'origin' };
const resources: BranchResources = { currentBranch: 'main', operationState: '', remotes: ['origin', 'backup'], branches: [{ name: 'origin/feature', remote: 'origin', branch: 'feature', sha: branch.sha }] };
const plan: RepositoryBranchPlan = { token: 'token', operation: 'create', name: 'feature', newName: 'new', sha: branch.sha, head: 'b'.repeat(40), stateToken: 'state', currentBranch: 'main', target: 'main', targetSha: 'c'.repeat(40), commits: [{ sha: 'b'.repeat(40), subject: 'First' }, { sha: 'c'.repeat(40), subject: 'Second' }] };
const ok = <T,>(result: T | null): CustomResponse<T> => ({ success: true, result, messages: ['Completed'], errors: [] });
function deferred<T>() { let resolve!: (value: T) => void; let reject!: (error: Error) => void; const promise = new Promise<T>((ok, fail) => { resolve = ok; reject = fail; }); return { promise, resolve, reject }; }
let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { view?.unmount(); view = undefined; vi.unstubAllGlobals(); });
function render(operation: BranchOperation = 'create', item = branch, options = resources) { view = mount(BranchActionDialog, { props: { repositoryId: 2, workspaceId: 1, branch: item, operation, resources: options }, global: { stubs: { Dialog: { props: ['visible', 'closable'], emits: ['update:visible'], template: '<div class="actions-dialog"><slot /></div>' } } } }); return view; }
function mock() { return { prepare: vi.spyOn(RepositoryBranchApiService.prototype, 'prepare').mockImplementation(async request => ok({ ...plan, ...request, rewritesHistory: request.operation === 'rebase' || request.operation === 'interactiveRebase', published: request.operation === 'rebase' })), execute: vi.spyOn(RepositoryBranchApiService.prototype, 'execute').mockResolvedValue(ok('done')) }; }
async function fill(page: VueWrapper) {
  if (page.find('.branch-new-name').exists()) await page.get('.branch-new-name').setValue(' new ');
  if (page.find('.branch-remote').exists()) await page.get('.branch-remote').setValue('backup');
  if (page.find('.branch-remote-name').exists()) await page.get('.branch-remote-name').setValue('destination');
  if (page.find('.branch-base').exists()) await page.get('.branch-base').setValue('main');
  if (page.find('.branch-destination').exists()) await page.get('.branch-destination').setValue('C:/worktrees/new');
  if (page.find('textarea').exists()) await page.get('textarea').setValue('Tag message');
  if (page.find('.branch-upstream').exists()) await page.get('.branch-upstream').setValue('refs/remotes/origin/feature');
  if (page.find('.branch-delete-confirmation').exists()) await page.get('.branch-delete-confirmation').setValue('origin/feature');
}
it.each(['create', 'rename', 'delete', 'checkout', 'worktree', 'fastForward', 'push', 'merge', 'rebase', 'tag', 'setUpstream', 'unsetUpstream', 'fetch', 'pull', 'continue', 'abort', 'checkoutRemote', 'deleteRemote'] as const)('reviews %s before executing its captured request and reports completion', async operation => {
  const api = mock(); const page = render(operation, ['pull', 'checkoutRemote', 'deleteRemote'].includes(operation) ? remoteBranch : branch); await fill(page);
  await page.get('form').trigger('submit'); await flushPromises(); expect(api.execute).not.toHaveBeenCalled();
  expect(page.get('.branch-dialog').text()).toContain(branch.sha);
  await page.get('.branch-confirm').trigger('click'); await flushPromises();
  expect(api.execute).toHaveBeenCalledWith(api.prepare.mock.calls[0]![0], 'token', expect.any(AbortSignal)); expect(page.emitted('changed')![0]).toEqual(['Completed']); expect(page.emitted('close')).toBeTruthy();
  if (operation === 'create') expect(api.prepare.mock.calls[0]![0]).toEqual({ repositoryId: 2, workspaceId: 1, operation, name: 'feature', newName: 'new' });
  if (operation === 'deleteRemote') expect(api.prepare.mock.calls[0]![0]).toMatchObject({ isRemote: true, confirmation: 'origin/feature' });
});
it('uses actual HTTP language propagation and never executes before review', async () => {
  const minimal = { token: plan.token, operation: plan.operation, name: plan.name, newName: plan.newName, sha: plan.sha };
  const fetchMock = vi.fn(async (url: string) => new Response(JSON.stringify(ok(url.endsWith('/Prepare') ? minimal : 'new')))); vi.stubGlobal('fetch', fetchMock); useLanguageStore().setLanguage('es');
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises(); expect(fetchMock).toHaveBeenCalledTimes(1);
  await page.get('.branch-confirm').trigger('click'); await flushPromises(); expect(fetchMock).toHaveBeenLastCalledWith('/api/RepositoryBranch/Execute', expect.objectContaining({ headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' } }));
});
it('opens a provider PR form without publishing or executing Git mutations', async () => {
  const api = mock(); api.prepare.mockResolvedValue(ok({ ...plan, operation: 'pullRequest', url: 'https://github.com/owner/repo/compare/main...feature?expand=1' }));
  const page = render('pullRequest', remoteBranch); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  expect(page.get('a').attributes('target')).toBe('_blank'); expect(page.get('a').attributes('rel')).toContain('noopener'); expect(page.find('.branch-confirm').exists()).toBe(false); expect(api.execute).not.toHaveBeenCalled();
});
it('reviews local PR fields, remote worktrees and empty remote defaults', async () => {
  const api = mock(); const page = render('pullRequest'); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  expect(api.prepare.mock.calls[0]![0]).toMatchObject({ remote: 'backup', remoteBranch: 'destination', baseBranch: 'main' }); page.unmount();
  const worktree = render('worktree', remoteBranch); await fill(worktree); await worktree.get('form').trigger('submit'); await flushPromises(); expect(api.prepare.mock.calls[1]![0]).toMatchObject({ newName: 'new', destination: 'C:/worktrees/new', isRemote: true }); worktree.unmount();
  const empty = render('push', branch, { ...resources, remotes: [], currentBranch: '' });
  expect(empty.find('.branch-remote').exists()).toBe(true);
});
it('reorders and edits the interactive todo, revalidates its unchanged base state and executes the reviewed sequence', async () => {
  const api = mock(); const page = render('interactiveRebase'); await page.get('form').trigger('submit'); await flushPromises();
  await page.findAll('.rebase-steps button')[1]!.trigger('click'); await page.findAll('.rebase-steps button')[2]!.trigger('click');
  await page.findAll('.rebase-steps select')[1]!.setValue('fixup');
  await page.get('.branch-confirm').trigger('click'); await flushPromises();
  expect(api.prepare).toHaveBeenCalledTimes(2); expect(api.prepare.mock.calls[1]![0].steps).toEqual([{ sha: 'b'.repeat(40), action: 'pick' }, { sha: 'c'.repeat(40), action: 'fixup' }]); expect(api.execute).toHaveBeenCalledTimes(1);
});
it('rejects an interactive plan whose state changed before final confirmation without executing', async () => {
  const api = mock(); const page = render('interactiveRebase'); await page.get('form').trigger('submit'); await flushPromises();
  api.prepare.mockResolvedValueOnce(ok({ ...plan, stateToken: 'changed' })); await page.get('.branch-confirm').trigger('click'); await flushPromises(); expect(api.execute).not.toHaveBeenCalled(); expect(page.emitted('uncertain')).toBeUndefined(); expect(page.get('[role="alert"]').text()).toBeTruthy();
});
it.each([new ApiError('Localized'), new Error('private'), null])('preserves editable fields after failed preflight or execution: %s', async error => {
  const api = mock(); if (error) api.prepare.mockRejectedValueOnce(error); else api.prepare.mockResolvedValueOnce(ok<RepositoryBranchPlan>(null));
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises(); expect(page.get('[role="alert"]').text()).not.toContain('private'); expect(page.get<HTMLInputElement>('input').element.value).toBe(' new ');
  await page.get('form').trigger('submit'); await flushPromises(); if (error) api.execute.mockRejectedValueOnce(error); else api.execute.mockResolvedValueOnce(ok<string>(null));
  await page.get('.branch-confirm').trigger('click'); await flushPromises(); expect(page.emitted('uncertain')).toBeTruthy(); expect(page.get<HTMLInputElement>('input').element.value).toBe(' new ');
});
it('blocks duplicate submissions and prevents closing during execution, but permits cancelling an idle review', async () => {
  const api = mock(); const pending = deferred<CustomResponse<string>>(); api.execute.mockReturnValueOnce(pending.promise);
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  await page.get('.branch-confirm').trigger('click'); await page.get('.branch-confirm').trigger('click'); page.getComponent(Dialog).vm.$emit('update:visible', false); await flushPromises(); expect(page.emitted('close')).toBeUndefined(); expect(api.execute).toHaveBeenCalledTimes(1);
  pending.resolve(ok('new')); await flushPromises(); page.getComponent(Dialog).vm.$emit('update:visible', false); expect(page.emitted('close')).toBeTruthy();
});
it.each(['prepare', 'execute', 'interactive'].flatMap(stage => [false, true].map(failed => ({ stage, failed }))))('aborts pending $stage on unmount and ignores late response (failed=$failed)', async ({ stage, failed }) => {
  const api = mock(); const preparing = deferred<CustomResponse<RepositoryBranchPlan>>(); const executing = deferred<CustomResponse<string>>(); const page = render(stage === 'interactive' ? 'interactiveRebase' : 'create'); await fill(page);
  if (stage === 'prepare') api.prepare.mockReturnValueOnce(preparing.promise);
  await page.get('form').trigger('submit'); await flushPromises();
  if (stage === 'execute') api.execute.mockReturnValueOnce(executing.promise);
  if (stage === 'interactive') api.prepare.mockReturnValueOnce(preparing.promise);
  if (stage !== 'prepare') await page.get('.branch-confirm').trigger('click');
  const signal = stage === 'execute' ? api.execute.mock.calls[0]![2]! : api.prepare.mock.calls.at(-1)![1]!;
  page.unmount(); view = undefined; expect(signal.aborted).toBe(true);
  if (stage === 'execute') { if (failed) executing.reject(new Error('late')); else executing.resolve(ok('new')); }
  else { if (failed) preparing.reject(new Error('late')); else preparing.resolve(ok(plan)); }
  await flushPromises(); expect(page.emitted('changed')).toBeUndefined();
});
