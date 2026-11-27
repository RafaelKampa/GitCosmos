import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ConflictResolver from './ConflictResolver.vue';
import { RepositoryConflictApiService } from '../../../core/repositories/repository-conflict-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { RepositoryConflict } from '../../../core/repositories/models/repository-conflict.model';
const token = 'a'.repeat(64); const version = { kind: 'text' as const, text: 'code', sha: 'b'.repeat(40), mode: '100644' };
const conflict: RepositoryConflict = { path: 'file.txt', operation: 'merge', token, base: version, ours: version, theirs: version, working: { ...version, text: '<<<<<<< HEAD\nours\n=======\ntheirs\n>>>>>>> branch' } };
const ok = <T,>(result: T | null) => ({ success: true, result, errors: [], messages: [] }); let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); }); afterEach(() => { view?.unmount(); view = undefined; vi.unstubAllGlobals(); });
function mock() { return { read: vi.spyOn(RepositoryConflictApiService.prototype, 'read').mockResolvedValue(ok(conflict)), prepare: vi.spyOn(RepositoryConflictApiService.prototype, 'prepare').mockResolvedValue(ok({ token: 'reviewed', path: 'file.txt', action: 'edit', removesFile: false, kind: 'text', text: 'resolved' })), resolve: vi.spyOn(RepositoryConflictApiService.prototype, 'resolve').mockResolvedValue(ok(true)), external: vi.spyOn(RepositoryConflictApiService.prototype, 'external').mockResolvedValue(ok(true)) }; }
function render() { view = mount(ConflictResolver, { props: { repositoryId: 2, workspaceId: 1, path: 'file.txt' }, global: { stubs: { Dialog: { name: 'Dialog', template: '<div class="dialog"><slot /></div>' } } } }); return view; }
it('shows all three versions and reviews a captured edit before saving and staging', async () => {
  const api = mock(); const page = render(); await flushPromises(); expect(page.findAll('.conflict-versions section')).toHaveLength(3);
  await page.get('textarea').setValue('resolved\r\n'); await page.get('.conflict-prepare').trigger('click'); await flushPromises(); expect(api.resolve).not.toHaveBeenCalled(); expect(page.get('.dialog').text()).toContain('resolved'); expect(page.get('.conflict-action').attributes('disabled')).toBeDefined();
  await page.get('.conflict-confirm').trigger('click'); await flushPromises(); expect(api.resolve).toHaveBeenCalledWith({ file: { repositoryId: 2, workspaceId: 1, path: 'file.txt' }, action: 'edit', content: 'resolved\n', token: 'reviewed' }, expect.any(AbortSignal)); expect(page.emitted('resolved')).toHaveLength(1);
});
it.each(['ours', 'theirs', 'delete', 'stage'] as const)('reviews %s including absent, binary and external resolutions', async action => {
  const api = mock(); api.read.mockResolvedValue(ok({ ...conflict, operation: 'rebase', base: { ...version, kind: 'absent', text: '' }, ours: { ...version, kind: 'binary', text: '' }, theirs: { ...version, kind: 'tooLarge', text: '' }, working: { ...version, kind: 'binary', text: '' } }));
  api.prepare.mockResolvedValue(ok({ token: 'reviewed', path: 'file.txt', action, removesFile: action === 'delete', kind: 'binary', text: '' })); const page = render(); await flushPromises(); expect(page.text()).toContain('Durante rebase'); expect(page.find('textarea').exists()).toBe(false);
  await page.get('select').setValue(action); await page.get('.conflict-prepare').trigger('click'); await flushPromises(); expect(api.prepare.mock.calls[0]![0].content).toBe('');
  expect(page.get('.dialog').text()).toContain(action === 'delete' ? 'remove o arquivo' : 'Resolução binária'); await page.get('.conflict-cancel').trigger('click'); expect(page.find('.dialog').exists()).toBe(false);
});
it('opens the working file externally and reloads only on explicit refresh', async () => {
  const api = mock(); const page = render(); await flushPromises(); await page.get('.conflict-external').trigger('click'); await flushPromises(); expect(api.external).toHaveBeenCalledWith({ repositoryId: 2, workspaceId: 1, path: 'file.txt' }, expect.any(AbortSignal)); expect(page.text()).toContain('Arquivo aberto');
  await page.get('.conflict-refresh').trigger('click'); await flushPromises(); expect(api.read).toHaveBeenCalledTimes(2);
  api.read.mockResolvedValueOnce(ok({ ...conflict, working: { ...version, kind: 'absent', text: '' } })); await page.get('.conflict-refresh').trigger('click'); await flushPromises(); expect(page.get('.conflict-external').attributes('disabled')).toBeDefined();
});
it.each(['read', 'prepare', 'resolve', 'external'] as const)('reports %s failures and prevents blind retry after an uncertain write', async target => {
  const api = mock(); api[target].mockRejectedValueOnce(new ApiError('Localized error')); const page = render(); await flushPromises();
  if (target === 'external') await page.get('.conflict-external').trigger('click');
  if (target === 'prepare' || target === 'resolve') { await page.get('.conflict-prepare').trigger('click'); await flushPromises(); if (target === 'resolve') await page.get('.conflict-confirm').trigger('click'); }
  await flushPromises(); expect(page.get('[role="alert"]').text()).toBe('Localized error');
  if (target === 'resolve') { expect(page.emitted('uncertain')).toHaveLength(1); expect(page.get('.conflict-prepare').attributes('disabled')).toBeDefined(); }
  await page.get('.conflict-refresh').trigger('click'); await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false);
});
it.each(['read', 'prepare', 'resolve', 'external'] as const)('rejects incomplete %s responses', async target => {
  const api = mock(); api[target].mockResolvedValueOnce(ok(null)); const page = render(); await flushPromises();
  if (target === 'external') await page.get('.conflict-external').trigger('click');
  if (target === 'prepare' || target === 'resolve') { await page.get('.conflict-prepare').trigger('click'); await flushPromises(); if (target === 'resolve') await page.get('.conflict-confirm').trigger('click'); }
  await flushPromises(); expect(page.get('[role="alert"]').text()).toContain('resolução');
});
it.each(['read', 'prepare', 'resolve', 'external'].flatMap(stage => [false, true].map(failed => ({ stage: stage as 'read' | 'prepare' | 'resolve' | 'external', failed }))))('ignores late $stage results on a new file (failed=$failed)', async ({ stage, failed }) => {
  const api = mock(); let resolve!: (value: never) => void; let reject!: (cause: Error) => void;
  api[stage].mockReturnValueOnce(new Promise<never>((ok, fail) => { resolve = ok; reject = fail; })); const page = render(); await flushPromises();
  if (stage === 'external') await page.get('.conflict-external').trigger('click');
  if (stage === 'prepare' || stage === 'resolve') { await page.get('.conflict-prepare').trigger('click'); await flushPromises(); if (stage === 'resolve') await page.get('.conflict-confirm').trigger('click'); }
  await flushPromises(); await page.setProps({ path: 'another.txt' }); await flushPromises();
  if (failed) reject(new Error('late')); else resolve(ok(null) as never); await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false); expect(page.emitted('resolved')).toBeUndefined();
});
it('blocks duplicate confirmations, closes review and reloads language through the real service', async () => {
  const api = mock(); let resolve!: (value: Awaited<ReturnType<RepositoryConflictApiService['resolve']>>) => void; api.resolve.mockReturnValueOnce(new Promise(done => { resolve = done; }));
  const page = render(); await flushPromises(); await page.get('.conflict-prepare').trigger('click'); await flushPromises(); page.getComponent({ name: 'Dialog' }).vm.$emit('update:visible', false); await flushPromises(); expect(page.find('.dialog').exists()).toBe(false);
  await page.get('.conflict-prepare').trigger('click'); await flushPromises(); const confirm = page.get('.conflict-confirm'); await confirm.trigger('click'); confirm.element.dispatchEvent(new Event('click', { bubbles: true })); await flushPromises(); expect(api.resolve).toHaveBeenCalledTimes(1); resolve(ok(true)); await flushPromises();
  api.read.mockRestore(); const fetch = vi.fn(async () => new Response(JSON.stringify(ok(conflict)))); vi.stubGlobal('fetch', fetch); useLanguageStore().setLanguage('es'); await flushPromises(); expect(fetch).toHaveBeenLastCalledWith(expect.any(String), expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
});
it('prevents repeated review and native actions while preparation is running', async () => {
  const api = mock(); let resolve!: (value: Awaited<ReturnType<RepositoryConflictApiService['prepare']>>) => void;
  api.prepare.mockReturnValueOnce(new Promise(done => { resolve = done; })); const page = render(); await flushPromises();
  const review = page.get('.conflict-prepare'); await review.trigger('click'); await flushPromises();
  review.element.dispatchEvent(new Event('click', { bubbles: true })); page.get('.conflict-external').element.dispatchEvent(new Event('click', { bubbles: true }));
  await flushPromises(); expect(api.prepare).toHaveBeenCalledTimes(1); expect(api.external).not.toHaveBeenCalled();
  resolve(ok({ token: 'reviewed', path: 'file.txt', action: 'edit', removesFile: false, kind: 'text', text: 'resolved' })); await flushPromises(); expect(page.find('.dialog').exists()).toBe(true);
});
it('ignores stale confirm clicks after closing review and refuses retries until an uncertain result is refreshed', async () => {
  const api = mock(); const page = render(); await flushPromises(); await page.get('.conflict-prepare').trigger('click'); await flushPromises();
  const oldConfirm = page.get('.conflict-confirm').element; await page.get('.conflict-cancel').trigger('click'); oldConfirm.dispatchEvent(new Event('click', { bubbles: true })); await flushPromises(); expect(api.resolve).not.toHaveBeenCalled();
  api.resolve.mockRejectedValueOnce(new Error('private diagnostics')); await page.get('.conflict-prepare').trigger('click'); await flushPromises(); await page.get('.conflict-confirm').trigger('click'); await flushPromises();
  const count = api.prepare.mock.calls.length; page.get('.conflict-prepare').element.dispatchEvent(new Event('click', { bubbles: true })); await flushPromises(); expect(api.prepare).toHaveBeenCalledTimes(count); expect(page.get('[role="alert"]').text()).not.toContain('private');
});
it.each(['read', 'prepare', 'resolve', 'external'] as const)('cancels a pending %s action on unmount and ignores its completion', async stage => {
  const api = mock(); let reject!: (cause: Error) => void; api[stage].mockReturnValueOnce(new Promise<never>((_, fail) => { reject = fail; })); const page = render(); await flushPromises();
  if (stage === 'external') await page.get('.conflict-external').trigger('click');
  if (stage === 'prepare' || stage === 'resolve') { await page.get('.conflict-prepare').trigger('click'); await flushPromises(); if (stage === 'resolve') await page.get('.conflict-confirm').trigger('click'); }
  await flushPromises(); const signal = api[stage].mock.calls[0]!.at(-1) as AbortSignal; page.unmount(); view = undefined; expect(signal.aborted).toBe(true); reject(new Error('late')); await flushPromises();
});
