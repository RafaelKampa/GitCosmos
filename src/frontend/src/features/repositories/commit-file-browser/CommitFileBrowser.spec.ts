import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import CommitFileBrowser from './CommitFileBrowser.vue';
import CommitTreeNode from './CommitTreeNode.vue';
import DiffViewer from '../diff-viewer/DiffViewer.vue';
import { CommitExplorerApiService } from '../../../core/repositories/commit-explorer-api.service';
import { RepositoryHistoryApiService } from '../../../core/repositories/repository-history-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { CommitDetails } from '../../../core/repositories/models/repository-history.model';
const sha = 'a'.repeat(40); const details: CommitDetails = { commit: { sha, parents: [], author: 'Author', email: '', authoredAt: '', subject: 'subject', message: '', refs: '' }, files: [{ path: 'folder/new.cs', originalPath: null, status: 'A' }, { path: 'old.cs', originalPath: null, status: 'D' }] };
const ok = <T,>(result: T | null) => ({ success: true, result, errors: [], messages: [] });
let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { view?.unmount(); view = undefined; vi.unstubAllGlobals(); });
function mock() {
  return { tree: vi.spyOn(CommitExplorerApiService.prototype, 'tree').mockResolvedValue(ok([{ path: 'folder/new.cs', mode: '100644', sha }, { path: 'unchanged.cs', mode: '100644', sha }])), file: vi.spyOn(CommitExplorerApiService.prototype, 'file').mockResolvedValue(ok({ path: 'folder/new.cs', kind: 'text', text: '<script>safe</script>\nnext', base64: '', fullPath: 'C:/repo/folder/new.cs' })), diff: vi.spyOn(RepositoryHistoryApiService.prototype, 'getDiff').mockResolvedValue(ok({ path: 'folder/new.cs', area: 'working', kind: 'text', patch: '@@ -0,0 +1 @@\n+new' })) };
}
function render(mode: 'tree' | 'changes' = 'tree') { view = mount(CommitFileBrowser, { props: { repositoryId: 2, workspaceId: 1, details, mode, focusPath: '' }, global: { stubs: { CommitFileMenu: true } } }); return view; }
it('uses the selected language through the real snapshot and diff HTTP services', async () => {
  const api = mock(); api.tree.mockRestore(); api.file.mockRestore(); api.diff.mockRestore();
  const fetch = vi.fn(async (url: string) => new Response(JSON.stringify(ok(url.includes('/Tree?')
    ? [{ path: 'folder/new.cs', mode: '100644', sha }]
    : url.includes('/GetDiff?') ? { path: 'folder/new.cs', area: 'working', kind: 'text', patch: '@@ -0,0 +1 @@\n+new' }
    : { path: 'folder/new.cs', kind: 'text', text: 'snapshot', base64: '', fullPath: 'C:/repo/folder/new.cs' }))));
  vi.stubGlobal('fetch', fetch); useLanguageStore().setLanguage('es');
  const page = render(); await flushPromises(); expect(page.get('.snapshot-code').text()).toBe('snapshot');
  expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/CommitExplorer/File?'), expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  await page.setProps({ mode: 'changes' }); await flushPromises();
  expect(page.findComponent(DiffViewer).exists()).toBe(true);
  expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('/RepositoryHistory/GetDiff?'), expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  useLanguageStore().setLanguage('en'); await flushPromises();
  expect(fetch).toHaveBeenLastCalledWith(expect.any(String), expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
});
it('shows all snapshot files, literal content, filtering, folder toggles and focused selection', async () => {
  const api = mock(); const page = render(); await flushPromises(); expect(page.findAll('.commit-tree-select')).toHaveLength(2); expect(page.find('script').exists()).toBe(false); expect(page.get('.snapshot-code').text()).toContain('<script>safe</script>');
  await page.get('.commit-folder').trigger('click'); expect(page.findAll('.commit-tree-select')).toHaveLength(1); await page.get('.commit-folder').trigger('click');
  await page.get('input').setValue('unchanged'); expect(page.findAll('.commit-tree-select')).toHaveLength(1); await page.get('.commit-tree-select').trigger('click'); await flushPromises(); expect(api.file).toHaveBeenLastCalledWith(expect.objectContaining({ sha, path: 'unchanged.cs' }), expect.any(AbortSignal));
  await page.get('input').setValue(''); await page.setProps({ focusPath: 'folder/new.cs' }); await flushPromises(); expect(page.get('.commit-tree-select[aria-pressed="true"]').attributes('data-path')).toBe('folder/new.cs');
  page.getComponent(CommitTreeNode).vm.$emit('showTree', 'folder/new.cs'); expect(page.emitted('showTree')!.at(-1)).toEqual(['folder/new.cs']);
  await page.get('.commit-tree-resize').trigger('keydown', { key: 'ArrowRight' }); expect(page.attributes('style')).toContain('32%');
});
it('shows only changed paths and committed diffs in Changes without loading the whole tree', async () => {
  const api = mock(); const page = render('changes'); await flushPromises(); expect(api.tree).not.toHaveBeenCalled(); expect(page.findAll('.commit-tree-select')).toHaveLength(2); expect(page.findComponent(DiffViewer).exists()).toBe(true); expect(page.get('.file-status.deleted').text()).toBe('−');
  useLanguageStore().setLanguage('en'); await flushPromises(); expect(api.diff).toHaveBeenCalledTimes(2);
});
it.each(['binary', 'submodule'] as const)('handles %s objects without rendering code', async kind => {
  const api = mock(); api.file.mockResolvedValue(ok({ path: 'folder/new.cs', kind, text: '', base64: '', fullPath: '' })); const page = render(); await flushPromises(); expect(page.find('.snapshot-code').exists()).toBe(false); expect(page.find('[role="alert"]').exists()).toBe(false);
});
it('bounds preview lines while preserving the complete snapshot response for downloads', async () => {
  const api = mock(); api.file.mockResolvedValueOnce(ok({ path: 'folder/new.cs', kind: 'text', text: 'line\n'.repeat(4001), base64: '', fullPath: '' }));
  const page = render(); await flushPromises(); expect(page.findAll('.snapshot-code li')).toHaveLength(4000); expect(page.text()).toContain('Prévia limitada');
});
it.each(['tree', 'file', 'diff'] as const)('shows a recoverable %s failure', async target => {
  const api = mock(); api[target].mockRejectedValueOnce(new ApiError('Localized failure')); const page = render(target === 'diff' ? 'changes' : 'tree'); await flushPromises(); expect(page.get('[role="alert"]').text()).toBe('Localized failure'); await page.findAll('button').find(button => button.text() === 'Tentar novamente')!.trigger('click'); await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false);
});
it.each(['tree', 'file', 'diff'] as const)('rejects incomplete %s responses', async target => {
  const api = mock(); api[target].mockResolvedValueOnce(ok(null)); const page = render(target === 'diff' ? 'changes' : 'tree'); await flushPromises(); expect(page.get('[role="alert"]').text()).toContain('arquivo');
});
it('ignores obsolete requests and handles empty snapshots and missing focus paths', async () => {
  const api = mock(); let resolve!: (value: Awaited<ReturnType<CommitExplorerApiService['tree']>>) => void;
  api.tree.mockReturnValueOnce(new Promise(done => { resolve = done; })); const page = render(); await page.setProps({ details: { ...details, commit: { ...details.commit, sha: 'b'.repeat(40) } }, focusPath: 'missing' }); await flushPromises(); resolve(ok([])); await flushPromises(); expect(page.findAll('.commit-tree-select')).toHaveLength(2);
  api.tree.mockResolvedValue(ok([])); await page.setProps({ details: { ...details, commit: { ...details.commit, sha: 'c'.repeat(40) } } }); await flushPromises(); expect(page.findAll('.commit-tree-select')).toHaveLength(0);
});
it.each(['tree', 'file', 'diff'].flatMap(stage => [false, true].map(failed => ({ stage: stage as 'tree' | 'file' | 'diff', failed }))))('discards stale $stage results (failed=$failed)', async ({ stage, failed }) => {
  const api = mock(); let resolve!: (value: never) => void; let reject!: (cause: Error) => void;
  const pending = new Promise<never>((ok, fail) => { resolve = ok; reject = fail; }); api[stage].mockReturnValueOnce(pending);
  const page = render(stage === 'diff' ? 'changes' : 'tree'); await flushPromises();
  await page.setProps({ repositoryId: 3 }); await flushPromises();
  if (failed) reject(new Error('late private')); else resolve(ok([]) as never);
  await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false);
});
