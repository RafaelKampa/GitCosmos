import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import Dialog from 'primevue/dialog';
import RepositoryCommitForm from './RepositoryCommitForm.vue';
import { RepositoryEditApiService } from '../../../core/repositories/repository-edit-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { GitFileChange } from '../../../core/repositories/models/repository-changes.model';
import type { RepositoryEditPlan, RepositoryEditResult } from '../../../core/repositories/models/repository-edit.model';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
const file: GitFileChange = { path: 'file.txt', originalPath: null, indexStatus: 'M', workTreeStatus: 'M', isUntracked: false, isConflicted: false, isSubmodule: false };
const plan: RepositoryEditPlan = { token: 'token', operation: 'commit', paths: ['file.txt'], fileCount: 1, head: 'head', amend: false, published: false, pushDestination: null };
const result: RepositoryEditResult = { operation: 'commit', head: 'sha', committed: true, pushAttempted: false, pushSucceeded: false, warning: null };
const ok = <T,>(result: T): CustomResponse<T> => ({ success: true, result, messages: ['Completed'], errors: [] });
function deferred<T>() { let resolve!: (value: T) => void; let reject!: (error: Error) => void; const promise = new Promise<T>((ok, fail) => { resolve = ok; reject = fail; }); return { promise, resolve, reject }; }
let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { wrapper?.unmount(); wrapper = undefined; vi.unstubAllGlobals(); });
function render(files: readonly GitFileChange[] = [file], hasCommits = true) {
  const view = mount(RepositoryCommitForm, { props: { repositoryId: 1, workspaceId: 0, files, hasCommits }, global: { stubs: { Dialog: { props: ['visible', 'closable'], emits: ['update:visible'], template: '<div v-if="visible" class="review-dialog"><slot /></div>' } } } }); wrapper = view; return view;
}
function mock() {
  return { prepare: vi.spyOn(RepositoryEditApiService.prototype, 'prepare').mockResolvedValue(ok(plan)), execute: vi.spyOn(RepositoryEditApiService.prototype, 'execute').mockResolvedValue(ok(result)) };
}
async function fill(view: VueWrapper) { await view.get('input:not([type="checkbox"])').setValue(' Subject '); await view.get('textarea').setValue('Description'); }

it('reviews the exact staged file count and commits the captured message only after confirmation', async () => {
  const api = mock(); const view = render([file, { ...file, path: 'new', indexStatus: '.', workTreeStatus: '?' }, { ...file, path: 'conflict', isConflicted: true }]);
  expect(view.get('.commit-submit').text()).toContain('1'); expect(view.get('.commit-submit').attributes('disabled')).toBeDefined();
  await fill(view); await view.get('form').trigger('submit'); await flushPromises();
  expect(api.prepare).toHaveBeenCalledWith({ repositoryId: 1, workspaceId: 0, operation: 'commit', paths: [], subject: 'Subject', description: 'Description', amend: false, push: false }, expect.any(AbortSignal));
  expect(api.execute).not.toHaveBeenCalled(); expect(view.get('.review-files').text()).toContain('file.txt');
  await view.get('.commit-confirm').trigger('click'); await flushPromises();
  expect(api.execute).toHaveBeenCalledWith(api.prepare.mock.calls[0]![0], 'token', expect.any(AbortSignal));
  expect(view.emitted('changed')![0]).toEqual([result, 'Completed']); expect(view.get<HTMLInputElement>('input:not([type="checkbox"])').element.value).toBe('');
  expect(view.find('.review-dialog').exists()).toBe(false);
});

it('reviews published amend and Commit and Push with its explicit destination and preserves partial success', async () => {
  const api = mock(); api.prepare.mockResolvedValue(ok({ ...plan, amend: true, published: true, pushDestination: 'origin/main' }));
  api.execute.mockResolvedValue(ok({ ...result, pushAttempted: true, warning: 'Commit saved, push failed' }));
  const view = render(); await fill(view); await view.get('input[type="checkbox"]').setValue(true);
  expect(view.text()).toContain('altera seu SHA');
  await view.get('.commit-options-toggle').trigger('click'); expect(view.get('.commit-options-toggle').attributes('aria-expanded')).toBe('true');
  await view.get('.commit-options button').trigger('click'); await flushPromises();
  expect(api.prepare.mock.calls[0]![0]).toMatchObject({ amend: true, push: true }); expect(view.text()).toContain('origin/main'); expect(view.text()).toContain('referência remota');
  await view.get('.commit-confirm').trigger('click'); await flushPromises();
  expect(view.emitted('changed')![0]![0]).toMatchObject({ committed: true, warning: 'Commit saved, push failed' });
  expect(view.get<HTMLInputElement>('input[type="checkbox"]').element.checked).toBe(false);
});

it('keeps the draft after stage/unstage and after cancelling a commit review', async () => {
  const api = mock(); api.execute.mockResolvedValue(ok({ ...result, committed: false, operation: 'stage' }));
  const view = render(); await fill(view); await view.vm.move({ operation: 'stage', paths: ['file.txt'] }); await flushPromises();
  expect(api.prepare.mock.calls[0]![0]).toMatchObject({ operation: 'stage', paths: ['file.txt'], subject: '', description: '', amend: false });
  expect(view.get<HTMLInputElement>('input:not([type="checkbox"])').element.value).toBe(' Subject ');
  await view.vm.move({ operation: 'unstage', paths: ['file.txt'] }); await flushPromises();
  expect(api.prepare.mock.calls[1]![0].operation).toBe('unstage');
  await view.get('form').trigger('submit'); await flushPromises(); view.getComponent(Dialog).vm.$emit('update:visible', false); await flushPromises();
  expect(view.find('.review-dialog').exists()).toBe(false); expect(view.get<HTMLInputElement>('input:not([type="checkbox"])').element.value).toBe(' Subject ');
  await view.get('.commit-options-toggle').trigger('click'); await view.get('form').trigger('keydown', { key: 'Escape' }); expect(view.find('.commit-options').exists()).toBe(false);
});

it('allows message-only amend with a previous commit and prevents it for unborn repositories', async () => {
  const view = render([], false); await fill(view); expect(view.get('.commit-submit').attributes('disabled')).toBeDefined(); expect(view.get('input[type="checkbox"]').attributes('disabled')).toBeDefined();
  await view.setProps({ hasCommits: true }); await view.get('input[type="checkbox"]').setValue(true); expect(view.get('.commit-submit').attributes('disabled')).toBeUndefined();
});

it.each([new ApiError('Localized'), new Error('private'), null])('preserves fields after failed or incomplete preflight: %s', async failure => {
  const api = mock(); if (failure) api.prepare.mockRejectedValue(failure); else api.prepare.mockResolvedValue({ ...ok(plan), result: null });
  const view = render(); await fill(view); await view.get('form').trigger('submit'); await flushPromises();
  expect(view.get('[role="alert"]').text()).toBe(failure instanceof ApiError ? failure.message : useLanguageStore().t('editError'));
  expect(api.execute).not.toHaveBeenCalled(); expect(view.get<HTMLInputElement>('input:not([type="checkbox"])').element.value).toBe(' Subject ');
});

it.each([new ApiError('State changed'), new Error('private'), null])('requires a new review after failed or incomplete execution: %s', async failure => {
  const api = mock(); if (failure) api.execute.mockRejectedValue(failure); else api.execute.mockResolvedValue({ ...ok(result), result: null });
  const view = render(); await fill(view); await view.get('form').trigger('submit'); await flushPromises(); await view.get('.commit-confirm').trigger('click'); await flushPromises();
  expect(view.find('.review-dialog').exists()).toBe(false); expect(view.get('[role="alert"]').text()).toBe(failure instanceof ApiError ? failure.message : useLanguageStore().t('editError'));
  expect(view.emitted('changed')).toBeUndefined();
});

it('reports errors from the stage execution path without clearing the draft', async () => {
  const api = mock(); api.execute.mockRejectedValue(new ApiError('stage failed')); const view = render(); await fill(view);
  await view.vm.move({ operation: 'stage', paths: ['file.txt'] }); expect(view.get('[role="alert"]').text()).toBe('stage failed');
});

it('blocks duplicate preflight and confirmation requests and closing during writes', async () => {
  const api = mock(); const pending = deferred<CustomResponse<RepositoryEditPlan>>(); api.prepare.mockReturnValueOnce(pending.promise);
  const view = render(); await fill(view); await view.get('form').trigger('submit'); await view.get('form').trigger('submit'); expect(api.prepare).toHaveBeenCalledTimes(1);
  pending.resolve(ok(plan)); await flushPromises(); await view.vm.move({ operation: 'stage', paths: ['file.txt'] }); expect(api.prepare).toHaveBeenCalledTimes(1);
  const writing = deferred<CustomResponse<RepositoryEditResult>>(); api.execute.mockReturnValueOnce(writing.promise);
  const button = view.get('.commit-confirm').element; button.dispatchEvent(new MouseEvent('click', { bubbles: true })); button.dispatchEvent(new MouseEvent('click', { bubbles: true })); await flushPromises();
  view.getComponent(Dialog).vm.$emit('update:visible', false); await flushPromises(); expect(view.find('.review-dialog').exists()).toBe(true); expect(api.execute).toHaveBeenCalledTimes(1);
  writing.resolve(ok(result)); await flushPromises(); expect(view.find('.review-dialog').exists()).toBe(false);
});

it.each(['prepare', 'execute', 'confirm'] as const)('aborts %s when leaving and ignores late success or failure', async phase => {
  for (const success of [true, false]) {
    const api = mock(); const pending = deferred<CustomResponse<RepositoryEditPlan & RepositoryEditResult>>();
    const method = phase === 'prepare' ? api.prepare : api.execute; method.mockReturnValueOnce(pending.promise);
    const view = render(); await fill(view);
    if (phase === 'execute') void view.vm.move({ operation: 'stage', paths: ['file.txt'] });
    else await view.get('form').trigger('submit');
    await flushPromises(); if (phase === 'confirm') { await view.get('.commit-confirm').trigger('click'); await flushPromises(); }
    const call = method.mock.calls[0]!; view.unmount(); wrapper = undefined; expect((call[call.length - 1] as AbortSignal).aborted).toBe(true);
    if (success) pending.resolve(ok({ ...plan, ...result })); else pending.reject(new Error('cancelled')); await flushPromises(); expect(view.emitted('changed')).toBeUndefined(); vi.restoreAllMocks();
  }
});

it('uses the current language in real preflight HTTP requests', async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(ok(plan)))); vi.stubGlobal('fetch', fetchMock); useLanguageStore().setLanguage('es');
  const view = render(); await fill(view); await view.get('form').trigger('submit'); await flushPromises();
  expect(fetchMock).toHaveBeenCalledWith('/api/RepositoryEdit/Prepare', expect.objectContaining({ headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' } }));
});
