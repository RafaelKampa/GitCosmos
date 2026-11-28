import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import LineStaging from './LineStaging.vue';
import DiffViewer from '../diff-viewer/DiffViewer.vue';
import LocalFileMenu from '../local-file-menu/LocalFileMenu.vue';
import { RepositoryLinesApiService } from '../../../core/repositories/repository-lines-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
const diff = { path: 'file.txt', area: 'working' as const, kind: 'text' as const, patch: '@@ -1 +1 @@\n-old\n+new\n' };
const snapshot = { diff, canSelect: true, token: 'a'.repeat(64), reason: '' };
const plan = { token: 'reviewed', path: 'file.txt', area: 'working' as const, lineCount: 2, result: 'old\n', removesEntry: false };
const ok = <T,>(result: T | null) => ({ success: true, result, errors: [], messages: [] }); let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); }); afterEach(() => { view?.unmount(); view = undefined; vi.unstubAllGlobals(); });
function mock() { return { read: vi.spyOn(RepositoryLinesApiService.prototype, 'read').mockResolvedValue(ok(snapshot)), prepare: vi.spyOn(RepositoryLinesApiService.prototype, 'prepare').mockResolvedValue(ok(plan)), execute: vi.spyOn(RepositoryLinesApiService.prototype, 'execute').mockResolvedValue(ok(true)) }; }
function render(disabled = false) { view = mount(LineStaging, { props: { repositoryId: 2, workspaceId: 1, diff, disabled }, global: { stubs: { LocalFileMenu: true, Dialog: { name: 'Dialog', template: '<div class="dialog"><slot /></div>' } } } }); return view; }
async function action(page: VueWrapper, operation = 'move') { page.getComponent(DiffViewer).vm.$emit('action', operation, [1, 2]); await flushPromises(); }
it('loads canonical selection automatically and stages exact line IDs without a second dialog', async () => {
  const api = mock(); const page = render(); await flushPromises();
  expect(api.read).toHaveBeenCalledWith({ repositoryId: 2, workspaceId: 1, path: 'file.txt', area: 'working' }, expect.any(AbortSignal));
  expect(page.find('input').exists()).toBe(false); page.getComponent(DiffViewer).vm.$emit('selection', [2]); await action(page);
  expect(api.prepare).toHaveBeenCalledWith({ file: { repositoryId: 2, workspaceId: 1, path: 'file.txt', area: 'working' }, lines: [1, 2], token: snapshot.token, operation: 'move' }, expect.any(AbortSignal));
  expect(api.execute).toHaveBeenCalledWith(expect.objectContaining({ lines: [1, 2] }), 'reviewed', expect.any(AbortSignal)); expect(page.emitted('changed')).toHaveLength(1); expect(page.find('.dialog').exists()).toBe(false);
});
it('reviews discard separately and captures the reviewed request before confirmation', async () => {
  const api = mock(); const page = render(); await flushPromises(); await action(page, 'discard'); expect(api.execute).not.toHaveBeenCalled(); expect(page.get('.dialog').text()).toContain('índice será preservado');
  expect(page.get('.lines-preview').text()).toBe('old'); await page.get('.lines-confirm').trigger('click'); await flushPromises(); expect(api.execute).toHaveBeenCalledWith(expect.objectContaining({ operation: 'discard' }), 'reviewed', expect.any(AbortSignal));
});
it('supports file deletion review and closes discard without executing a stale confirmation', async () => {
  const api = mock(); api.prepare.mockResolvedValue(ok({ ...plan, removesEntry: true })); const page = render(); await flushPromises(); await action(page, 'discard'); expect(page.get('.dialog').text()).toContain('removido do disco');
  const oldConfirm = page.get('.lines-confirm').element; await page.get('.lines-cancel').trigger('click'); oldConfirm.dispatchEvent(new Event('click', { bubbles: true })); await flushPromises(); expect(api.execute).not.toHaveBeenCalled();
  await action(page, 'discard'); page.getComponent({ name: 'Dialog' }).vm.$emit('update:visible', false); await flushPromises(); expect(page.find('.dialog').exists()).toBe(false);
});
it('retains the original diff for unsupported files and skips binary selection requests', async () => {
  const api = mock(); api.read.mockResolvedValue(ok({ ...snapshot, canSelect: false, reason: 'Whole-file only' })); const page = render(); await flushPromises(); expect(page.text()).toContain('Whole-file only'); expect(page.getComponent(DiffViewer).props('diff')).toEqual(diff);
  await action(page); expect(api.prepare).not.toHaveBeenCalled(); await page.setProps({ diff: { ...diff, kind: 'binary' } }); expect(api.read).toHaveBeenCalledTimes(1);
});
it.each(['read', 'prepare', 'execute'] as const)('shows localized %s failures and refreshes explicitly', async target => {
  const api = mock(); api[target].mockRejectedValueOnce(new ApiError('Localized error')); const page = render(); await flushPromises(); if (target !== 'read') await action(page);
  expect(page.findAll('[role="alert"]').some(row => row.text() === 'Localized error')).toBe(true);
  if (target === 'execute') { expect(page.emitted('uncertain')).toHaveLength(1); const count = api.prepare.mock.calls.length; await action(page); expect(api.prepare).toHaveBeenCalledTimes(count); }
  await page.get('.lines-start').trigger('click'); await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false);
});
it.each(['read', 'prepare', 'execute'] as const)('rejects incomplete %s responses', async target => {
  const api = mock(); api[target].mockResolvedValueOnce(ok(null) as never); const page = render(); await flushPromises(); if (target !== 'read') await action(page);
  expect(page.findAll('[role="alert"]').some(row => row.text().includes('Não foi possível'))).toBe(true);
});
it.each(['read', 'prepare', 'execute'].flatMap(stage => [false, true].map(failed => ({ stage: stage as 'read' | 'prepare' | 'execute', failed }))))('ignores stale $stage results (failed=$failed)', async ({ stage, failed }) => {
  const api = mock(); let resolve!: (value: never) => void; let reject!: (cause: Error) => void;
  api[stage].mockReturnValueOnce(new Promise<never>((ok, fail) => { resolve = ok; reject = fail; })); const page = render(); await flushPromises(); if (stage !== 'read') await action(page);
  const signal = api[stage].mock.calls[0]!.at(-1) as AbortSignal; await page.setProps({ repositoryId: 3 }); expect(signal.aborted).toBe(true);
  if (failed) reject(new Error('late')); else resolve(ok(null) as never); await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false); expect(page.emitted('changed')).toBeUndefined();
});
it.each(['read', 'prepare', 'execute'] as const)('cancels pending %s on unmount', async stage => {
  const api = mock(); let reject!: (cause: Error) => void; api[stage].mockReturnValueOnce(new Promise<never>((_, fail) => { reject = fail; })); const page = render(); await flushPromises(); if (stage !== 'read') await action(page);
  const signal = api[stage].mock.calls[0]!.at(-1) as AbortSignal; page.unmount(); view = undefined; expect(signal.aborted).toBe(true); reject(new Error('late')); await flushPromises();
});
it('prevents duplicate actions and closing discard while confirmation is running', async () => {
  const api = mock(); let finish!: (value: Awaited<ReturnType<RepositoryLinesApiService['prepare']>>) => void; api.prepare.mockReturnValueOnce(new Promise(done => { finish = done; }));
  const page = render(); await flushPromises(); await action(page, 'discard'); await action(page); expect(api.prepare).toHaveBeenCalledTimes(1); finish(ok(plan)); await flushPromises();
  let complete!: (value: Awaited<ReturnType<RepositoryLinesApiService['execute']>>) => void; api.execute.mockReturnValueOnce(new Promise(done => { complete = done; }));
  const button = page.get('.lines-confirm').element; await page.get('.lines-confirm').trigger('click'); button.dispatchEvent(new Event('click', { bubbles: true })); page.getComponent({ name: 'Dialog' }).vm.$emit('update:visible', false); await flushPromises(); expect(page.find('.dialog').exists()).toBe(true); expect(api.execute).toHaveBeenCalledTimes(1);
  complete(ok(true)); await flushPromises();
});
it('handles discard confirmation errors without allowing a duplicate destructive attempt', async () => {
  const api = mock(); const page = render(); await flushPromises(); await action(page, 'discard'); api.execute.mockRejectedValue(new Error('private'));
  await page.get('.lines-confirm').trigger('click'); await flushPromises(); expect(page.emitted('uncertain')).toHaveLength(1); expect(page.text()).not.toContain('private'); expect(page.find('.dialog').exists()).toBe(false);
});
it('opens the native-selection menu with its exact range, IDs and snapshot token', async () => {
  mock(); const page = render(); await flushPromises(); const value = { ids: [2], start: 1, end: 1, text: 'new', x: 10, y: 20 };
  page.getComponent(DiffViewer).vm.$emit('context', value); await flushPromises(); const menu = page.getComponent(LocalFileMenu); expect(menu.props('selection')).toEqual(value); expect(menu.props('snapshotToken')).toBe(snapshot.token);
  menu.vm.$emit('busy', true); await flushPromises(); page.getComponent(DiffViewer).vm.$emit('context', { ...value, text: 'ignored' }); await flushPromises(); expect(menu.props('selection')).toEqual(value);
  menu.vm.$emit('busy', false); menu.vm.$emit('close'); await flushPromises(); expect(page.findComponent(LocalFileMenu).exists()).toBe(false);
});
it('defers selection loading while disabled, blocks empty actions and uses the current HTTP language', async () => {
  const api = mock(); const page = render(true); await flushPromises(); expect(api.read).not.toHaveBeenCalled(); await action(page); expect(api.prepare).not.toHaveBeenCalled();
  await page.setProps({ disabled: false }); await flushPromises(); page.getComponent(DiffViewer).vm.$emit('action', 'move', []); await flushPromises(); expect(api.prepare).not.toHaveBeenCalled();
  api.read.mockRestore(); const fetch = vi.fn(async () => new Response(JSON.stringify(ok(snapshot)))); vi.stubGlobal('fetch', fetch); useLanguageStore().setLanguage('es'); await flushPromises(); expect(fetch).toHaveBeenCalledWith(expect.any(String), expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
});
