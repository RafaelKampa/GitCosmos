import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import CommitFileMenu from './CommitFileMenu.vue';
import { CommitExplorerApiService } from '../../../core/repositories/commit-explorer-api.service';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
const sha = 'a'.repeat(40); const parent = 'b'.repeat(40); const ok = <T,>(result: T | null) => ({ success: true, result, errors: [], messages: [] });
let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) }, userAgent: '' }); });
afterEach(() => { view?.unmount(); view = undefined; vi.unstubAllGlobals(); vi.restoreAllMocks(); });
function mock() {
  return {
    tools: vi.spyOn(CommitExplorerApiService.prototype, 'tools').mockResolvedValue(ok([{ id: 'vscode', name: 'VS Code' }])),
    file: vi.spyOn(CommitExplorerApiService.prototype, 'file').mockResolvedValue(ok({ path: 'file.txt', kind: 'text', text: 'hello', base64: 'aGVsbG8=', fullPath: 'C:/repo/file.txt' })),
    history: vi.spyOn(CommitExplorerApiService.prototype, 'history').mockResolvedValue(ok([{ sha, parents: [], author: 'Author', email: '', subject: 'Subject', message: '', authoredAt: '', refs: '' }])),
    blame: vi.spyOn(CommitExplorerApiService.prototype, 'blame').mockResolvedValue(ok([{ line: 1, sha, author: 'Author', time: '0', text: '<script>code</script>' }])),
    prepare: vi.spyOn(CommitExplorerApiService.prototype, 'prepare').mockResolvedValue(ok({ token: 'token', path: 'file.txt', sourceSha: sha, sourcePath: 'file.txt', exists: true })),
    restore: vi.spyOn(CommitExplorerApiService.prototype, 'restore').mockResolvedValue(ok(true)),
    desktop: vi.spyOn(CommitExplorerApiService.prototype, 'desktop').mockResolvedValue(ok(true)),
  };
}
function render(changed = true) { view = mount(CommitFileMenu, { props: { repositoryId: 2, workspaceId: 1, sha, path: 'file.txt', parent, changed }, attachTo: document.body, global: { stubs: { Teleport: true, Dialog: { name: 'Dialog', props: ['visible'], template: '<section class="dialog"><slot /></section>' } } } }); return view; }
async function open(page: VueWrapper) { await page.get('.file-options').trigger('click'); await flushPromises(); }
async function choose(page: VueWrapper, label: string) { await page.findAll('[role="menuitem"]').find(button => button.text() === label)!.trigger('click'); await flushPromises(); }
it('uses the selected language through the real tool discovery HTTP service', async () => {
  const api = mock(); api.tools.mockRestore();
  const fetch = vi.fn(async () => new Response(JSON.stringify(ok([{ id: 'vscode', name: 'VS Code' }]))));
  vi.stubGlobal('fetch', fetch); useLanguageStore().setLanguage('es'); const page = render(); await open(page);
  expect(fetch).toHaveBeenCalledWith(expect.stringContaining('/CommitExplorer/Tools?'), expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  expect(page.text()).toContain('VS Code');
});
it.each([['Abrir', 'open'], ['Mostrar no Explorador de Arquivos', 'reveal'], ['VS Code', 'diff']])('executes explicit %s action with the scoped file', async (label, action) => {
  const api = mock(); const page = render(); await open(page); await choose(page, label);
  expect(api.desktop).toHaveBeenCalledWith({ repositoryId: 2, workspaceId: 1, sha, path: 'file.txt' }, action, action === 'diff' ? 'vscode' : '', expect.any(AbortSignal));
  expect(page.get('[role="status"]').text()).toContain('concluída');
});
it('opens a deleted file from the parent and copies complete or relative paths', async () => {
  const api = mock(); const page = render(); await page.setProps({ deleted: true });
  await open(page); await choose(page, 'Abrir'); expect(api.desktop.mock.calls[0]![0].sha).toBe(parent);
  await open(page); await choose(page, 'Copiar caminho'); expect(navigator.clipboard.writeText).toHaveBeenCalledWith('file.txt');
  await open(page); await choose(page, 'Copiar caminho completo'); expect(navigator.clipboard.writeText).toHaveBeenCalledWith('C:/repo/file.txt'); expect(api.file.mock.calls[0]![0].sha).toBe(parent);
});
it('sends Show in File Tree without a native operation and uses a smaller menu for snapshots', async () => {
  const api = mock(); const page = render(); await open(page); await choose(page, 'Mostrar na árvore de arquivos'); expect(page.emitted('showTree')!.at(-1)).toEqual(['file.txt']); expect(api.desktop).not.toHaveBeenCalled();
  await page.setProps({ changed: false }); await open(page); expect(page.find('details').exists()).toBe(false); expect(page.text()).not.toContain('Mostrar na árvore');
});
it.each([false, true])('reviews restore (%s) and requires a second explicit confirmation', async before => {
  const api = mock(); if (before) api.prepare.mockResolvedValue(ok({ token: 'token', path: 'file.txt', sourceSha: '', sourcePath: 'file.txt', exists: false }));
  const page = render(); await open(page); await choose(page, before ? 'Estado antes deste commit' : 'Estado neste commit'); expect(api.restore).not.toHaveBeenCalled(); expect(page.get('.dialog').text()).toContain('Alterações locais');
  if (before) expect(page.get('.dialog').text()).toContain('removido');
  await page.get('.confirm-file-restore').trigger('click'); await flushPromises(); expect(api.restore).toHaveBeenCalledWith(expect.objectContaining({ sha }), before, 'token', expect.any(AbortSignal)); expect(useExplorerPreferences().repositoryVersions[2]).toBe(1);
});
it.each(['history', 'blame'])('shows %s and navigates to its immutable commit', async kind => {
  mock(); const page = render(); await open(page); await choose(page, kind === 'history' ? 'Histórico do arquivo' : 'Blame / Linha do tempo'); expect(page.find('script').exists()).toBe(false);
  await page.get('.dialog button').trigger('click'); await flushPromises(); expect(useExplorerPreferences().historyNavigation[2]!.sha).toBe(sha);
});
it('bounds history results and tolerates empty history and blame responses', async () => {
  const api = mock(); const commit = { sha, parents: [], author: 'Author', email: '', subject: 'subject', message: '', authoredAt: '', refs: '' };
  api.history.mockResolvedValueOnce(ok(Array.from({ length: 101 }, (_, i) => ({ ...commit, sha: String(i) })))); const page = render(); await open(page); await choose(page, 'Histórico do arquivo'); expect(page.findAll('.file-history li')).toHaveLength(100); expect(page.text()).toContain('primeiros 100');
  await page.setProps({ path: 'other.txt' }); api.history.mockResolvedValueOnce(ok(null)); await open(page); await choose(page, 'Histórico do arquivo'); expect(page.findAll('.file-history li')).toHaveLength(0);
  await page.setProps({ path: 'third.txt' }); api.blame.mockResolvedValueOnce(ok(null)); await open(page); await choose(page, 'Blame / Linha do tempo'); expect(page.findAll('.blame-table tr')).toHaveLength(0);
});
it.each([new ApiError('Localized error'), new Error('private')])('reports failed actions without exposing native details: %s', async failure => {
  const api = mock(); api.desktop.mockRejectedValueOnce(failure); const page = render(); await open(page); await choose(page, 'Abrir'); expect(page.get('[role="alert"]').text()).not.toContain('private');
});
it('handles unavailable tools, incomplete responses, clipboard errors and restoration failures', async () => {
  const api = mock(); api.tools.mockResolvedValueOnce(ok(null)); const page = render(); await open(page); expect(page.text()).toContain('Nenhum aplicativo'); await page.get('.file-options').trigger('click'); expect(page.find('[role="menu"]').exists()).toBe(false);
  api.file.mockResolvedValueOnce(ok(null)); await open(page); await choose(page, 'Copiar caminho completo'); expect(page.get('[role="alert"]').text()).toContain('arquivo');
  vi.mocked(navigator.clipboard.writeText).mockRejectedValueOnce(new Error()); await open(page); await choose(page, 'Copiar caminho'); expect(page.find('[role="alert"]').exists()).toBe(true);
  api.prepare.mockResolvedValueOnce(ok(null)); await open(page); await choose(page, 'Estado neste commit'); expect(page.find('.dialog').exists()).toBe(false);
  api.restore.mockRejectedValueOnce(new ApiError('State changed')); await open(page); await choose(page, 'Estado neste commit'); await page.get('.confirm-file-restore').trigger('click'); await flushPromises(); expect(page.get('.dialog').text()).toContain('State changed');
});
it('downloads exact snapshot bytes and cleans up the object URL', async () => {
  mock(); const create = vi.fn().mockReturnValue('blob:test'); const revoke = vi.fn(); vi.stubGlobal('URL', class extends URL { static createObjectURL = create; static revokeObjectURL = revoke; });
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {}); const page = render(); await open(page); await choose(page, 'Salvar como…'); expect(create).toHaveBeenCalledWith(expect.any(Blob)); expect(click).toHaveBeenCalled();
  await new Promise(done => setTimeout(done, 1050)); expect(revoke).toHaveBeenCalledWith('blob:test');
});
it('closes on Escape, outside pointer and language/context changes and ignores late tool results', async () => {
  const api = mock(); const page = render(); await open(page);
  for (const key of ['ArrowDown', 'ArrowUp', 'Home', 'End', 'a']) await page.get('[role="menu"]').trigger('keydown', { key });
  await page.get('[role="menu"]').trigger('keydown', { key: 'Escape' }); expect(page.find('[role="menu"]').exists()).toBe(false);
  await open(page); document.body.dispatchEvent(new Event('pointerdown', { bubbles: true })); await flushPromises(); expect(page.find('[role="menu"]').exists()).toBe(false);
  let resolve!: (value: Awaited<ReturnType<CommitExplorerApiService['tools']>>) => void; api.tools.mockReturnValueOnce(new Promise(done => { resolve = done; })); await page.get('.file-options').trigger('click'); await flushPromises();
  await page.setProps({ sha: 'c'.repeat(40) }); resolve(ok([])); await flushPromises(); expect(page.find('[role="menu"]').exists()).toBe(false);
  useLanguageStore().setLanguage('en'); await flushPromises(); expect(page.get('.file-options').attributes('aria-label')).toContain('File options');
});
it('disables missing root versions, reports malformed native results and skips submodule downloads', async () => {
  const api = mock(); const page = render(); await page.setProps({ deleted: true, parent: '' }); await open(page);
  expect(page.findAll('[role="menuitem"]').filter(button => button.attributes('disabled') !== undefined).length).toBeGreaterThan(1);
  await page.get('.file-options').trigger('click'); await page.setProps({ deleted: false });
  api.desktop.mockResolvedValueOnce(ok(false)); await open(page); await choose(page, 'Abrir'); expect(page.get('[role="alert"]').text()).toContain('arquivo');
  api.file.mockResolvedValueOnce(ok({ path: 'module', kind: 'submodule', text: sha, base64: '', fullPath: 'C:/repo/module' })); await open(page); await choose(page, 'Salvar como…'); expect(page.get('[role="alert"]').text()).toContain('submódulo');
  api.restore.mockResolvedValueOnce(ok(false)); await open(page); await choose(page, 'Estado neste commit'); await page.get('.confirm-file-restore').trigger('click'); await flushPromises(); expect(page.get('.dialog').text()).toContain('arquivo');
});
it('closes its dialog explicitly and ignores failed requests after unmount', async () => {
  const api = mock(); const page = render(); await open(page); await choose(page, 'Estado neste commit');
  page.getComponent({ name: 'Dialog' }).vm.$emit('update:visible', false); await flushPromises();
  expect(page.find('.dialog').exists()).toBe(false);
  let reject!: (cause: Error) => void; api.tools.mockReturnValueOnce(new Promise((_, fail) => { reject = fail; })); await page.get('.file-options').trigger('click'); await flushPromises(); page.unmount(); view = undefined; reject(new Error('late')); await flushPromises();
});
it('blocks duplicate confirmations and invalidates repository state once per successful restore', async () => {
  const api = mock(); let resolve!: (value: Awaited<ReturnType<CommitExplorerApiService['restore']>>) => void;
  api.restore.mockReturnValueOnce(new Promise(done => { resolve = done; }));
  const page = render(); await open(page); await choose(page, 'Estado neste commit');
  const confirm = page.get('.confirm-file-restore'); await confirm.trigger('click'); await flushPromises();
  expect(confirm.attributes('disabled')).toBeDefined();
  confirm.element.dispatchEvent(new Event('click', { bubbles: true })); await flushPromises();
  expect(api.restore).toHaveBeenCalledTimes(1);
  resolve(ok(true)); await flushPromises(); expect(useExplorerPreferences().repositoryVersions[2]).toBe(1);
  await open(page); await choose(page, 'Estado neste commit'); await page.get('.confirm-file-restore').trigger('click'); await flushPromises();
  expect(api.restore).toHaveBeenCalledTimes(2); expect(useExplorerPreferences().repositoryVersions[2]).toBe(2);
});
it.each([
  ['file', 'Copiar caminho completo'], ['history', 'Histórico do arquivo'], ['blame', 'Blame / Linha do tempo'],
  ['prepare', 'Estado neste commit'], ['desktop', 'Abrir'], ['restore', 'Estado neste commit'],
] as const)('ignores a completed %s action after the file context changes', async (method, label) => {
  const api = mock(); let resolve!: (value: never) => void;
  api[method].mockReturnValueOnce(new Promise<never>(done => { resolve = done; }));
  const page = render(); await open(page); await choose(page, label);
  if (method === 'restore') { await page.get('.confirm-file-restore').trigger('click'); await flushPromises(); }
  const signal = api[method].mock.calls[0]!.at(-1) as AbortSignal;
  await page.setProps({ path: 'another-file.txt' }); expect(signal.aborted).toBe(true);
  resolve(ok(null) as never); await flushPromises();
  expect(page.find('.dialog').exists()).toBe(false); expect(page.find('[role="status"]').exists()).toBe(false);
  expect(page.find('[role="alert"]').exists()).toBe(false); expect(useExplorerPreferences().repositoryVersions[2]).toBeUndefined();
  expect(navigator.clipboard.writeText).not.toHaveBeenCalled();
});
it.each(['Copiar caminho', 'Copiar caminho completo'])('ignores late clipboard feedback for %s after switching files', async label => {
  mock(); let resolve!: () => void;
  vi.mocked(navigator.clipboard.writeText).mockReturnValueOnce(new Promise<void>(done => { resolve = done; }));
  const page = render(); await open(page); await choose(page, label);
  await page.setProps({ path: 'another-file.txt' }); resolve(); await flushPromises();
  expect(page.find('[role="status"]').exists()).toBe(false);
});
it('keeps open submenus keyboard accessible and does not close on inside pointer events', async () => {
  mock(); const page = render(); await open(page);
  page.get('.file-options').element.dispatchEvent(new Event('pointerdown', { bubbles: true })); await flushPromises();
  expect(page.find('[role="menu"]').exists()).toBe(true);
  const submenu = page.get('details'); (submenu.element as HTMLDetailsElement).open = true;
  await page.get('[role="menu"]').trigger('keydown', { key: 'ArrowDown' });
  expect(page.find('[role="menu"]').exists()).toBe(true);
});
it('limits blame preview rows and reports the truncation explicitly', async () => {
  const NativeDateTimeFormat = Intl.DateTimeFormat;
  const formatter = vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(function (locales, options) {
    return new NativeDateTimeFormat(locales, options);
  });
  const api = mock(); api.blame.mockResolvedValueOnce(ok(Array.from({ length: 4001 }, (_, index) => ({ line: index + 1, sha, author: 'Author', time: '0', text: 'code' }))));
  const page = render(); await open(page); await choose(page, 'Blame / Linha do tempo');
  const table = page.get('.blame-table').element as HTMLTableElement;
  expect(table.rows.length).toBe(4000);
  expect(table.rows[0]!.querySelector('time')!.textContent).toBe(new Date(0).toLocaleString('pt-BR'));
  expect(table.rows[3999]!.cells[0]!.textContent).toBe('4000');
  expect(page.get('.dialog').text()).toContain('Prévia limitada');
  expect(formatter).toHaveBeenCalledTimes(1);
});
