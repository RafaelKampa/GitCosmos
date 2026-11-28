import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import LocalFileMenu from './LocalFileMenu.vue';
import { LocalFilesApiService } from '../../../core/repositories/local-files-api.service';
import { RepositoryLinesApiService } from '../../../core/repositories/repository-lines-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';
const sha = 'a'.repeat(40);
const commit = { sha, parents: [], author: 'Author', email: 'test@example.invalid', authoredAt: '2026-11-27T00:00:00Z', subject: 'Change', refs: '', message: 'Change' };
const info = { fullPath: 'C:\\repo\\folder\\a.cs', patch: 'diff --git a/file b/file\n', history: [commit], blame: [{ line: 1, sha, author: 'Author', time: '1700000000', text: '<tag>' }], tools: [{ id: 'vscode', name: 'VS Code' }], editors: [{ id: 'rider', name: 'Rider' }] };
const context = { path: 'folder/a.cs', area: 'working' as const, paths: ['folder/a.cs', 'b.cs'], all: ['folder/a.cs', 'b.cs', 'c.cs'], x: 10000, y: 10000 };
const selection = { ids: [3, 4], start: 8, end: 9, text: 'selected text', x: 10, y: 20 };
const ok = <T,>(result: T | null) => ({ success: true, result, errors: [], messages: [] }); let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); }); afterEach(() => { wrapper?.unmount(); wrapper = undefined; vi.unstubAllGlobals(); vi.useRealTimers(); });
function mock() { return { read: vi.spyOn(LocalFilesApiService.prototype, 'read').mockResolvedValue(ok(info)), prepare: vi.spyOn(LocalFilesApiService.prototype, 'prepare').mockResolvedValue(ok({ token: 'review', action: 'discard', paths: context.paths, preview: 'folder/a.cs\nb.cs' })), execute: vi.spyOn(LocalFilesApiService.prototype, 'execute').mockResolvedValue(ok(true)), desktop: vi.spyOn(LocalFilesApiService.prototype, 'desktop').mockResolvedValue(ok(true)), patch: vi.spyOn(RepositoryLinesApiService.prototype, 'prepare').mockResolvedValue(ok({ token: 'snapshot', path: context.path, area: 'working', lineCount: 2, result: 'new', removesEntry: false, patch: 'selected patch' })) }; }
function render(text = false, staged = false) { wrapper = mount(LocalFileMenu, { props: { repositoryId: 2, workspaceId: 1, context: { ...context, area: staged ? 'staged' : 'working' }, untracked: true, ...(text ? { selection, snapshotToken: 'snapshot' } : {}) }, attachTo: document.body, global: { stubs: { Teleport: true, Dialog: { name: 'Dialog', template: '<div class="dialog"><slot /></div>' } } } }); return wrapper; }
async function pick(page: VueWrapper, label: string) { const button = page.findAll('[role="menuitem"]').find(item => item.text().startsWith(label)); expect(button, label).toBeDefined(); await button!.trigger('click'); await flushPromises(); }
it.each([false, true])('offers Stage/Unstage and all-files actions on the correct side (staged=%s)', async staged => {
  const api = mock(); const page = render(false, staged); await flushPromises(); expect(page.find('[role="menu"]').exists()).toBe(true); expect(page.findAll('details')).toHaveLength(staged ? 1 : 2);
  expect(page.findAll('[role="menuitem"]').some(button => button.text().startsWith('Descartar'))).toBe(!staged);
  await pick(page, staged ? 'Unstage (' : 'Stage ('); expect(page.emitted('move')!.at(-1)).toEqual([{ operation: staged ? 'unstage' : 'stage', paths: context.paths }]); expect(api.execute).not.toHaveBeenCalled();
});
it.each([false, true])('moves all files for staged=%s', async staged => { mock(); const page = render(false, staged); await flushPromises(); await pick(page, staged ? 'Unstage de todos' : 'Stage de todos'); expect(page.emitted('move')!.at(-1)).toEqual([{ operation: staged ? 'unstage' : 'stage', paths: context.all }]); });
it.each(['Abrir', 'Explorador', 'VS Code'])('dispatches native file action %s with the scoped target', async label => {
  const api = mock(); const page = render(); await flushPromises();
  // Labels shared with the commit menu remain localized by the same catalog.
  const target = label === 'Explorador' ? page.findAll('[role="menuitem"]').find(item => item.text().includes('Explorador'))!.text() : label;
  await pick(page, target); expect(api.desktop).toHaveBeenCalledWith(expect.objectContaining({ repositoryId: 2, workspaceId: 1, paths: [context.path], action: label === 'Abrir' ? 'open' : label === 'VS Code' ? 'diff' : 'reveal' }), expect.any(AbortSignal));
});
it.each(['Descartar', 'Stash', 'Este arquivo', 'Arquivos com a mesma extensão', 'Esta pasta'])('reviews %s and only executes after confirming', async label => {
  const api = mock(); const action = label === 'Descartar' ? 'discard' : label === 'Stash' ? 'stash' : 'ignore'; api.prepare.mockResolvedValue(ok({ token: 'review', action, paths: context.paths, preview: 'reviewed paths' }));
  const page = render(); await flushPromises(); await pick(page, label); expect(api.execute).not.toHaveBeenCalled(); expect(page.get('.dialog').text()).toContain('reviewed paths');
  await page.get('.local-confirm').trigger('click'); await flushPromises(); expect(api.execute).toHaveBeenCalledWith(expect.objectContaining({ action, paths: context.paths, token: 'review' }), expect.any(AbortSignal)); expect(page.emitted('changed')).toHaveLength(1);
});
it.each(['Histórico', 'Blame'])('reads %s and allows navigating to the selected commit', async label => {
  const api = mock(); const page = render(); await flushPromises(); await pick(page, label); expect(api.read).toHaveBeenLastCalledWith(expect.objectContaining({ action: label === 'Blame' ? 'blame' : 'history' }), expect.any(AbortSignal)); expect(page.find('.dialog').exists()).toBe(true);
  if (label === 'Histórico') { await page.get('.local-preview button').trigger('click'); expect(useExplorerPreferences().repositoryTabs[2]).toBe('history'); expect(useExplorerPreferences().historyNavigation[2]!.sha).toBe(sha); }
  else { expect(page.get('table code').text()).toBe('<tag>'); expect(page.find('tag').exists()).toBe(false); }
});
it('copies selected relative and full paths without confusing the focused file with the multi-selection', async () => {
  mock(); const clipboard = vi.fn().mockResolvedValue(undefined); vi.stubGlobal('navigator', { clipboard: { writeText: clipboard } }); let page = render(); await flushPromises(); await pick(page, 'Copiar caminho completo'); expect(clipboard).toHaveBeenCalledWith('C:\\repo\\folder\\a.cs\nC:\\repo\\b.cs'); page.unmount(); wrapper = undefined;
  page = render(); await flushPromises(); const buttons = page.findAll('[role="menuitem"]'); const relative = buttons.find(button => button.text().startsWith('Copiar caminho') && !button.text().includes('completo'))!; await relative.trigger('click'); await flushPromises(); expect(clipboard).toHaveBeenLastCalledWith(context.paths.join('\n'));
});
it('exports selected files as a downloadable patch', async () => {
  mock(); const NativeURL = URL; const revoke = vi.fn(); vi.stubGlobal('URL', class extends NativeURL { static createObjectURL() { return 'blob:patch'; } static revokeObjectURL(value: string) { revoke(value); } }); const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  const page = render(); await flushPromises(); vi.useFakeTimers(); await pick(page, 'Salvar como patch'); expect(click).toHaveBeenCalledTimes(1); vi.advanceTimersByTime(1000); expect(revoke).toHaveBeenCalledWith('blob:patch');
});
it.each(['Rider', 'Histórico das linhas', 'Copiar como patch', 'Copiar'])('provides the native text-selection action %s', async label => {
  const api = mock(); const clipboard = vi.fn().mockResolvedValue(undefined); vi.stubGlobal('navigator', { clipboard: { writeText: clipboard } }); const page = render(true); await flushPromises(); expect(page.findAll('[role="menuitem"]')).toHaveLength(4); await pick(page, label);
  if (label === 'Rider') expect(api.desktop).toHaveBeenCalledWith(expect.objectContaining({ action: 'line', tool: 'rider', start: 8, end: 9 }), expect.any(AbortSignal));
  else if (label === 'Histórico das linhas') expect(api.read).toHaveBeenLastCalledWith(expect.objectContaining({ action: 'lineHistory', start: 8, end: 9 }), expect.any(AbortSignal));
  else if (label === 'Copiar como patch') { expect(api.patch).toHaveBeenCalledWith(expect.objectContaining({ lines: [3, 4], token: 'snapshot', exportPatch: true }), expect.any(AbortSignal)); expect(clipboard).toHaveBeenCalledWith('selected patch'); }
  else expect(clipboard).toHaveBeenCalledWith('selected text');
});
it('explains missing editors and history and disables invalid ignore and patch actions', async () => {
  const api = mock(); api.read.mockResolvedValue(ok({ ...info, tools: [], editors: [], history: [], blame: [] })); const page = render(); await page.setProps({ untracked: false }); await flushPromises(); expect(page.find('small').exists()).toBe(true);
  expect(page.findAll('details')[1]!.findAll('button').every(button => button.attributes('disabled') !== undefined)).toBe(true); await pick(page, 'Histórico'); expect(page.text()).toContain('Não há histórico'); page.unmount(); wrapper = undefined;
  const selected = render(true); await selected.setProps({ selection: { ...selection, ids: [] }, snapshotToken: undefined }); await flushPromises(); expect(selected.findAll('[role="menuitem"]').find(button => button.text() === 'Copiar como patch')!.attributes('disabled')).toBeDefined();
});
it('supports menu keyboard navigation, outside close and cancelling a reviewed action', async () => {
  mock(); const page = render(); await flushPromises(); const menu = page.get('[role="menu"]'); for (const key of ['ArrowDown', 'ArrowUp', 'Home', 'End', 'x']) await menu.trigger('keydown', { key });
  await page.get('summary').trigger('click'); await menu.trigger('keydown', { key: 'ArrowDown' }); await menu.trigger('keydown', { key: 'Escape' }); expect(page.emitted('close')).toBeDefined(); page.unmount(); wrapper = undefined;
  const second = render(); await flushPromises(); second.get('[role="menu"]').element.dispatchEvent(new Event('pointerdown', { bubbles: true })); expect(second.emitted('close')).toBeUndefined(); window.dispatchEvent(new Event('pointerdown')); expect(second.emitted('close')).toBeDefined(); second.unmount(); wrapper = undefined;
  const third = render(); await flushPromises(); await pick(third, 'Descartar'); const stale = third.get('.local-confirm').element; third.getComponent({ name: 'Dialog' }).vm.$emit('update:visible', false); await flushPromises(); stale.dispatchEvent(new Event('click', { bubbles: true })); expect(third.find('.dialog').exists()).toBe(false);
});
it.each(['read', 'prepare', 'execute', 'desktop'] as const)('reports %s failures, including uncertain mutations', async target => {
  const api = mock(); const page = render(); await flushPromises(); api[target].mockRejectedValueOnce(new ApiError('Localized error'));
  if (target === 'read') await pick(page, 'Histórico'); else if (target === 'desktop') await pick(page, 'Abrir'); else { await pick(page, 'Descartar'); if (target === 'execute') { await page.get('.local-confirm').trigger('click'); await flushPromises(); expect(page.emitted('uncertain')).toHaveLength(1); } }
  expect(page.findAll('[role="alert"]').some(item => item.text() === 'Localized error')).toBe(true);
});
it.each(['read', 'prepare', 'execute', 'desktop'] as const)('rejects incomplete %s responses', async target => {
  const api = mock(); const page = render(); await flushPromises(); api[target].mockResolvedValueOnce(ok(null) as never);
  if (target === 'read') await pick(page, 'Histórico'); else if (target === 'desktop') await pick(page, 'Abrir'); else { await pick(page, 'Descartar'); if (target === 'execute') { await page.get('.local-confirm').trigger('click'); await flushPromises(); } }
  expect(page.text()).toContain('Não foi possível concluir a ação');
});
it('prevents duplicate writes and refuses closing during a pending confirmation', async () => {
  const api = mock(); const page = render(); await flushPromises(); await pick(page, 'Descartar'); let resolve!: (value: ReturnType<typeof ok<boolean>>) => void; api.execute.mockReturnValueOnce(new Promise(done => { resolve = done; }));
  const button = page.get('.local-confirm').element; await page.get('.local-confirm').trigger('click'); button.dispatchEvent(new Event('click', { bubbles: true })); page.getComponent({ name: 'Dialog' }).vm.$emit('update:visible', false); await flushPromises(); expect(page.find('.dialog').exists()).toBe(true); expect(api.execute).toHaveBeenCalledTimes(1); resolve(ok(true)); await flushPromises();
});
it.each(['read', 'prepare', 'execute', 'desktop'] as const)('aborts pending %s on unmount and ignores late results', async target => {
  const api = mock(); const page = render(); await flushPromises(); let resolve!: (value: never) => void; api[target].mockReturnValueOnce(new Promise<never>(done => { resolve = done; }));
  if (target === 'read') await pick(page, 'Histórico'); else if (target === 'desktop') await pick(page, 'Abrir'); else { await pick(page, 'Descartar'); if (target === 'execute') { await page.get('.local-confirm').trigger('click'); await flushPromises(); } }
  const signal = api[target].mock.calls.at(-1)!.at(-1) as AbortSignal; page.unmount(); wrapper = undefined; expect(signal.aborted).toBe(true); resolve(ok(null) as never); await flushPromises(); expect(page.emitted('changed')).toBeUndefined();
});
it('uses current HTTP language callbacks and cancels context or language changes', async () => {
  const api = mock(); api.read.mockRestore(); const fetch = vi.fn(async () => new Response(JSON.stringify(ok(info)))); vi.stubGlobal('fetch', fetch); const page = render(); await flushPromises(); expect(fetch).toHaveBeenCalledWith('/api/LocalFiles/Read', expect.objectContaining({ headers: { 'Accept-Language': 'pt-BR', 'Content-Type': 'application/json' } }));
  useLanguageStore().setLanguage('es'); await flushPromises(); expect(page.emitted('close')).toBeDefined();
});

it.each([true, false])('reports incomplete or failed metadata discovery (failed=%s)', async failed => {
  const api = mock(); if (failed) api.read.mockRejectedValueOnce(new Error('private')); else api.read.mockResolvedValueOnce(ok(null));
  const page = render(); await flushPromises(); expect(page.text()).toContain('Não foi possível concluir a ação'); expect(page.text()).not.toContain('private');
});
it('copies full paths on POSIX and blocks disabled actions without changing the selection', async () => {
  const api = mock(); api.read.mockResolvedValue(ok({ ...info, fullPath: '/repo/folder/a.cs' })); const clipboard = vi.fn().mockResolvedValue(undefined); vi.stubGlobal('navigator', { clipboard: { writeText: clipboard } });
  const page = render(); await flushPromises(); await page.setProps({ disabled: true }); await pick(page, 'Stage ('); expect(page.emitted('move')).toBeUndefined(); await pick(page, 'Abrir'); expect(api.desktop).not.toHaveBeenCalled();
  await page.setProps({ disabled: false }); await pick(page, 'Copiar caminho completo'); expect(clipboard).toHaveBeenCalledWith('/repo/folder/a.cs\n/repo/b.cs');
});
it.each([true, false])('rejects a missing selected patch or failed clipboard (clipboard=%s)', async clipboardFailure => {
  const api = mock(); const clipboard = vi.fn().mockRejectedValue(new Error('private')); vi.stubGlobal('navigator', { clipboard: { writeText: clipboard } }); const page = render(true); await flushPromises();
  if (!clipboardFailure) api.patch.mockResolvedValueOnce(ok(null)); await pick(page, 'Copiar como patch'); expect(page.text()).toContain('Não foi possível concluir a ação'); expect(page.text()).not.toContain('private');
});
it.each(['copy', 'patch'] as const)('ignores a late selected %s result after context change', async action => {
  const api = mock(); let resolve!: (value: never) => void; const clipboard = vi.fn().mockReturnValue(new Promise<never>(done => { resolve = done; })); vi.stubGlobal('navigator', { clipboard: { writeText: clipboard } });
  const page = render(true); await flushPromises(); if (action === 'patch') api.patch.mockReturnValueOnce(new Promise<never>(done => { resolve = done; }));
  await pick(page, action === 'copy' ? 'Copiar' : 'Copiar como patch'); await page.setProps({ repositoryId: 9 }); resolve((action === 'copy' ? undefined : ok(null)) as never); await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false); expect(page.find('[role="menu"]').exists()).toBe(false);
});
it('limits history and blame previews and makes empty blame explicit', async () => {
  const api = mock(); api.read.mockResolvedValue(ok({ ...info, history: Array.from({ length: 101 }, (_, id) => ({ ...commit, sha: String(id).padStart(40, 'a') })) }));
  const page = render(); await flushPromises(); await pick(page, 'Histórico'); expect(page.findAll('.local-preview li')).toHaveLength(100); expect(page.text()).toContain('primeiros 100');
  page.unmount(); wrapper = undefined; api.read.mockResolvedValue(ok({ ...info, blame: [] })); const empty = render(); await flushPromises(); await pick(empty, 'Blame'); expect(empty.text()).toContain('Não há histórico');
});

it('keeps the result uncertain after a failed mutation and refuses a stale follow-up action', async () => {
  const api = mock(); const page = render(); await flushPromises(); const open = page.findAll('[role="menuitem"]').find(button => button.text() === 'Abrir')!.element;
  await pick(page, 'Descartar'); api.execute.mockRejectedValueOnce(new Error('private')); await page.get('.local-confirm').trigger('click'); await flushPromises(); open.dispatchEvent(new Event('click', { bubbles: true })); await flushPromises(); expect(api.desktop).not.toHaveBeenCalled(); expect(page.emitted('uncertain')).toHaveLength(1);
});
