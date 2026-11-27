import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import RepositoryRecovery from './RepositoryRecovery.vue';
import BranchActionDialog from '../repository-branches/BranchActionDialog.vue';
import { RepositoryBranchApiService } from '../../../core/repositories/repository-branch-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
const resources = { currentBranch: 'main', operationState: 'merge', remotes: [], branches: [] }; const ok = <T,>(result: T | null) => ({ success: true, result, errors: [], messages: [] }); let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); }); afterEach(() => { view?.unmount(); view = undefined; vi.unstubAllGlobals(); });
function render() { view = mount(RepositoryRecovery, { props: { repositoryId: 2, workspaceId: 1, conflictCount: 1, disabled: false }, global: { stubs: { BranchActionDialog: true } } }); return view; }
it.each(['merge', 'rebase'])('offers reviewed continuation and abort for %s and forwards busy/close/change', async operationState => {
  const api = vi.spyOn(RepositoryBranchApiService.prototype, 'getResources').mockResolvedValue(ok({ ...resources, operationState })); const page = render(); await flushPromises(); expect(page.get('.recovery-continue').attributes('disabled')).toBeDefined();
  await page.setProps({ conflictCount: 0 }); await flushPromises(); await page.get('.recovery-continue').trigger('click'); const dialog = page.getComponent(BranchActionDialog); expect(dialog.props()).toMatchObject({ operation: 'continue', branch: { name: 'main' } });
  dialog.vm.$emit('busy', true); expect(page.emitted('busy')!.at(-1)).toEqual([true]); dialog.vm.$emit('close'); await flushPromises(); expect(page.findComponent(BranchActionDialog).exists()).toBe(false);
  await page.get('.recovery-abort').trigger('click'); expect(page.getComponent(BranchActionDialog).props('operation')).toBe('abort'); page.getComponent(BranchActionDialog).vm.$emit('changed'); await flushPromises(); expect(page.emitted('changed')).toHaveLength(1);
  await page.get('.recovery-abort').trigger('click'); page.getComponent(BranchActionDialog).vm.$emit('uncertain'); await flushPromises(); expect(page.emitted('changed')).toHaveLength(2); expect(api.mock.calls.length).toBeGreaterThan(2);
  await page.setProps({ disabled: true }); expect(page.get('.recovery-abort').attributes('disabled')).toBeDefined();
});
it.each([new ApiError('Localized'), new Error('private'), null])('reports read failure and supports explicit retry: %s', async cause => {
  const api = vi.spyOn(RepositoryBranchApiService.prototype, 'getResources'); if (cause) api.mockRejectedValueOnce(cause); else api.mockResolvedValueOnce(ok(null)); api.mockResolvedValue(ok({ ...resources, operationState: '' }));
  const page = render(); await flushPromises(); expect(page.get('[role="alert"]').text()).not.toContain('private'); await page.get('.recovery-error button').trigger('click'); await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false); expect(page.find('.repository-recovery').exists()).toBe(false);
});
it.each([false, true])('ignores obsolete state reads (failed=%s) and uses the selected HTTP language', async failed => {
  let resolve!: (value: ReturnType<typeof ok<typeof resources>>) => void; let reject!: (cause: Error) => void;
  const api = vi.spyOn(RepositoryBranchApiService.prototype, 'getResources').mockReturnValueOnce(new Promise((ok, fail) => { resolve = ok; reject = fail; })).mockResolvedValue(ok(resources));
  const page = render(); await page.setProps({ repositoryId: 3 }); await flushPromises(); if (failed) reject(new Error('late')); else resolve(ok({ ...resources, operationState: '' })); await flushPromises(); expect(page.get('.repository-recovery').text()).toContain('merge');
  api.mockRestore(); const fetch = vi.fn(async () => new Response(JSON.stringify(ok(resources)))); vi.stubGlobal('fetch', fetch); useLanguageStore().setLanguage('en'); await flushPromises(); expect(fetch).toHaveBeenLastCalledWith(expect.any(String), expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
});
