import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import { nextTick } from 'vue';
import HorizontalScroll from '../../../shared/components/horizontal-scroll/HorizontalScroll.vue';
import RepositoryHistory from './RepositoryHistory.vue';
import DiffViewer from '../diff-viewer/DiffViewer.vue';
import { RepositoryHistoryApiService } from '../../../core/repositories/repository-history-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
import type { GitCommit, HistoryPage, CommitDetails, CommitDiff } from '../../../core/repositories/models/repository-history.model';

const commit: GitCommit = { sha: 'a'.repeat(40), parents: ['b'.repeat(40)], author: 'Author', email: 'test@example.invalid', authoredAt: '2026-11-22T12:00:00Z', subject: '<script>subject</script>', refs: 'refs/heads/main, tag: refs/tags/v1, refs/remotes/origin/main', message: '<img src=x onerror=alert(1)>\nBody' };
const ok = <T,>(result: T): CustomResponse<T> => ({ success: true, result, errors: [], messages: [] });
const page: HistoryPage = { commits: [commit], snapshot: 's', hasMore: false };
const details: CommitDetails = { commit, files: [{ path: 'new.txt', originalPath: 'old.txt', status: 'R100' }, { path: 'binary.dat', originalPath: null, status: 'A' }] };
const patch: CommitDiff = { path: 'new.txt', area: 'working', kind: 'text', patch: '@@ -1 +1 @@\n-old\n+new' };
function deferred<T>() {
  let resolve!: (value: T) => void; let reject!: (reason: Error) => void;
  const promise = new Promise<T>((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}
let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { wrapper?.unmount(); wrapper = undefined; vi.unstubAllGlobals(); });
function render() { wrapper = mount(RepositoryHistory, { props: { workspaceId: 1, repositoryId: 2 } }); return wrapper; }
function mock() {
  const list = vi.spyOn(RepositoryHistoryApiService.prototype, 'getHistory').mockResolvedValue(ok(page));
  const detail = vi.spyOn(RepositoryHistoryApiService.prototype, 'getCommit').mockResolvedValue(ok(details));
  const diff = vi.spyOn(RepositoryHistoryApiService.prototype, 'getDiff').mockResolvedValue(ok(patch));
  return { list, detail, diff };
}

it('loads pages, preserves graph edges, displays commit details and selects committed file diffs safely', async () => {
  const api = mock(); const pending = deferred<CustomResponse<HistoryPage>>();
  api.list.mockReturnValueOnce(pending.promise).mockResolvedValueOnce(ok({ ...page, commits: [{ ...commit, sha: 'b'.repeat(40), parents: [], refs: '' }] }));
  const view = render(); await nextTick();
  expect(view.get('[role="status"]').text()).toBe('Consultando histórico…');
  expect(view.text()).not.toContain('Nenhum commit disponível');
  expect(view.get('.history-refresh').attributes('disabled')).toBeDefined();
  expect(api.list).toHaveBeenCalledWith(1, 2, 0, null, expect.any(AbortSignal));
  pending.resolve(ok({ ...page, hasMore: true })); await flushPromises();
  expect(view.get('.reference-pill.remote').text()).toBe('origin/main');
  expect(view.get('.reference-pill.remote').attributes('title')).toContain('refs/remotes/origin/main');
  expect(view.get('.commit-author').text()).toContain('Author');
  expect(view.get('.commit-short-sha').text()).toBe('aaaaaaaa');
  expect(view.get('time').text()).toContain('2026');
  expect(view.find('script').exists()).toBe(false);
  await view.get('.history-more').trigger('click'); await flushPromises();
  expect(api.list).toHaveBeenLastCalledWith(1, 2, 1, 's', expect.any(AbortSignal));
  expect(view.findAll('.commit-choice')).toHaveLength(2); expect(view.findAll('svg path').length).toBeGreaterThan(0);
  const detailsPending = deferred<CustomResponse<CommitDetails>>(); api.detail.mockReturnValueOnce(detailsPending.promise);
  await view.get('.commit-choice').trigger('click'); expect(view.get('.commit-details [role="status"]').text()).toContain('histórico');
  detailsPending.resolve(ok(details)); await flushPromises();
  expect(view.get('.commit-message').text()).toContain('<img'); expect(view.find('.commit-message img').exists()).toBe(false);
  expect(view.get('.commit-comparison').text()).toContain('primeiro pai');
  expect(view.get('.commit-sha').text()).toBe(commit.sha);
  expect(view.get('.commit-files').text()).toContain('old.txt →');
  const diffPending = deferred<CustomResponse<CommitDiff>>(); api.diff.mockReturnValueOnce(diffPending.promise);
  await view.get('.commit-files button').trigger('click'); expect(view.get('.commit-details [role="status"]').text()).toContain('diferenças');
  diffPending.resolve(ok(patch)); await flushPromises();
  expect(api.diff).toHaveBeenLastCalledWith(1, 2, commit.sha, 'new.txt', expect.any(AbortSignal));
  expect(view.getComponent(DiffViewer).props('diff')).toEqual(patch);
  await view.get('.history-refresh').trigger('click'); await flushPromises();
  expect(view.find('.commit-sha').exists()).toBe(false);
});

it('shows empty repositories, root comparisons and commits without changed files', async () => {
  const api = mock(); api.list.mockResolvedValueOnce(ok({ ...page, commits: [] }));
  api.detail.mockResolvedValueOnce(ok({ commit: { ...commit, parents: [] }, files: [] }));
  const view = render(); await flushPromises(); expect(view.text()).toContain('Nenhum commit');
  await view.get('.history-refresh').trigger('click'); await flushPromises();
  await view.get('.commit-choice').trigger('click'); await flushPromises();
  expect(view.text()).toContain('árvore vazia'); expect(view.text()).toContain('Nenhum arquivo alterado');
});

it.each(['list', 'detail', 'diff'] as const)('handles API, network and incomplete responses and retry for %s', async target => {
  const api = mock();
  const method = target === 'list' ? api.list : target === 'detail' ? api.detail : api.diff;
  method.mockRejectedValueOnce(new ApiError('Localized failure'));
  method.mockRejectedValueOnce(new Error('private'));
  method.mockResolvedValueOnce({ success: true, result: null, errors: [], messages: [] });
  const view = render(); await flushPromises();
  if (target !== 'list') { await view.get('.commit-choice').trigger('click'); await flushPromises(); }
  if (target === 'diff') { await view.get('.commit-files button').trigger('click'); await flushPromises(); }
  const retry = target === 'list' ? '.history-retry' : target === 'detail' ? '.commit-retry' : '.commit-diff-retry';
  expect(view.get('[role="alert"]').text()).toBe('Localized failure');
  for (let attempt = 0; attempt < 3; attempt++) {
    await view.get(retry).trigger('click'); await flushPromises();
    if (attempt < 2) expect(view.get('[role="alert"]').text()).toBe('Não foi possível consultar o histórico do repositório.');
  }
  expect(view.find('[role="alert"]').exists()).toBe(false);
});

it('keeps loaded commits when loading another page fails and blocks duplicate requests', async () => {
  const api = mock(); const pending = deferred<CustomResponse<HistoryPage>>();
  api.list.mockResolvedValueOnce(ok({ ...page, hasMore: true })).mockReturnValueOnce(pending.promise);
  const view = render(); await flushPromises();
  const button = view.get('.history-more').element;
  button.dispatchEvent(new MouseEvent('click', { bubbles: true }));
  button.dispatchEvent(new MouseEvent('click', { bubbles: true })); await flushPromises();
  expect(api.list).toHaveBeenCalledTimes(2);
  pending.reject(new ApiError('References changed')); await flushPromises();
  expect(view.findAll('.commit-choice')).toHaveLength(1);
  expect(view.get('[role="alert"]').text()).toBe('References changed');
  await view.get('.history-refresh').trigger('click'); await flushPromises();
  expect(api.list).toHaveBeenLastCalledWith(1, 2, 0, null, expect.any(AbortSignal));
});

it.each([true, false])('aborts stale history on context changes and ignores late completion (success=%s)', async success => {
  const api = mock(); const pending = deferred<CustomResponse<HistoryPage>>();
  api.list.mockReturnValueOnce(pending.promise).mockResolvedValue(ok({ ...page, commits: [] }));
  const view = render(); await view.setProps({ workspaceId: 0, repositoryId: 3 }); await flushPromises();
  expect(api.list.mock.calls[0]![4]!.aborted).toBe(true);
  if (success) pending.resolve(ok(page)); else pending.reject(new Error('cancelled'));
  await flushPromises(); expect(view.find('.commit-choice').exists()).toBe(false);
});

it.each(['list', 'detail', 'diff'] as const)('aborts %s on unmount and ignores late success and failure', async target => {
  for (const success of [true, false]) {
    const api = mock(); const pending = deferred<CustomResponse<HistoryPage & CommitDetails & CommitDiff>>();
    const method = target === 'list' ? api.list : target === 'detail' ? api.detail : api.diff;
    // The pending response deliberately outlives this component; its shape is never consumed.
    method.mockReturnValueOnce(pending.promise);
    const view = render(); await flushPromises();
    if (target !== 'list') { await view.get('.commit-choice').trigger('click'); await flushPromises(); }
    if (target === 'diff') { await view.get('.commit-files button').trigger('click'); await flushPromises(); }
    const call = method.mock.calls[0]!;
    view.unmount(); wrapper = undefined;
    expect((call[call.length - 1] as AbortSignal).aborted).toBe(true);
    if (success) pending.resolve(ok({ ...page, ...details, ...patch })); else pending.reject(new Error('cancelled'));
    await flushPromises();
    vi.restoreAllMocks();
  }
});

it('aborts previous commit and file selections and ignores their late responses', async () => {
  const api = mock(); const pending = deferred<CustomResponse<CommitDetails>>();
  api.detail.mockReturnValueOnce(pending.promise);
  const view = render(); await flushPromises();
  await view.get('.commit-choice').trigger('click'); await view.get('.commit-choice').trigger('click'); await flushPromises();
  expect(api.detail.mock.calls[0]![3]!.aborted).toBe(true);
  pending.resolve(ok({ ...details, files: [] })); await flushPromises();
  expect(view.findAll('.commit-files button')).toHaveLength(2);
  const pendingDiff = deferred<CustomResponse<CommitDiff>>(); api.diff.mockReturnValueOnce(pendingDiff.promise);
  await view.get('.commit-files button').trigger('click');
  await view.findAll('.commit-files button')[1]!.trigger('click'); await flushPromises();
  expect(api.diff.mock.calls[0]![4]!.aborted).toBe(true);
  pendingDiff.resolve(ok({ ...patch, kind: 'binary' })); await flushPromises();
  expect(view.getComponent(DiffViewer).props('diff').kind).toBe('text');
});

it('uses the current language through HTTP and reloads history after a language change', async () => {
  const fetchMock = vi.fn().mockImplementation(async (url: string) => new Response(JSON.stringify(ok(url.includes('GetHistory') ? page : url.includes('GetCommit') ? details : patch))));
  vi.stubGlobal('fetch', fetchMock);
  useLanguageStore().setLanguage('es'); const view = render(); await flushPromises();
  await view.get('.commit-choice').trigger('click'); await flushPromises();
  await view.get('.commit-files button').trigger('click'); await flushPromises();
  expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('GetDiff'), expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  useLanguageStore().setLanguage('en'); await flushPromises();
  expect(fetchMock).toHaveBeenLastCalledWith(expect.stringContaining('GetHistory'), expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
  expect(view.findComponent(DiffViewer).exists()).toBe(false);
});

it('opens the diff immediately below its file and allows only one expanded file', async () => {
  const api = mock(); const view = render(); await flushPromises();
  await view.get('.commit-choice').trigger('click'); await flushPromises();
  const toggles = view.findAll('.commit-file-toggle');
  expect(toggles.every(toggle => toggle.attributes('aria-expanded') === 'false')).toBe(true);
  await toggles[0]!.trigger('click'); await flushPromises();
  expect(toggles[0]!.attributes('aria-expanded')).toBe('true');
  expect(view.findAll('.commit-file-diff')).toHaveLength(1);
  expect(view.get('.commit-file-diff').element.previousElementSibling).toBe(toggles[0]!.element);
  expect(view.get('.commit-file-diff').attributes('id')).toBe(toggles[0]!.attributes('aria-controls'));
  expect(view.get('.commit-file-diff').attributes('aria-labelledby')).toBe(toggles[0]!.attributes('id'));
  await toggles[1]!.trigger('click'); await flushPromises();
  expect(toggles[0]!.attributes('aria-expanded')).toBe('false');
  expect(toggles[1]!.attributes('aria-expanded')).toBe('true');
  expect(view.findAll('.commit-file-diff')).toHaveLength(1);
  expect(view.get('.commit-file-diff').element.previousElementSibling).toBe(toggles[1]!.element);
  await toggles[1]!.trigger('click'); await flushPromises();
  expect(view.find('.commit-file-diff').exists()).toBe(false);
  expect(api.diff).toHaveBeenCalledTimes(2);
  await toggles[0]!.trigger('click'); await flushPromises();
  expect(api.diff).toHaveBeenCalledTimes(3);
});

it('uses green plus, red minus and yellow modified badges in committed file accordions', async () => {
  const api = mock();
  api.detail.mockResolvedValue(ok({ ...details, files: [
    { path: 'added.txt', originalPath: null, status: 'A' },
    { path: 'removed.txt', originalPath: null, status: 'D' },
    { path: 'modified.txt', originalPath: null, status: 'M' },
  ] }));
  const view = render(); await flushPromises(); await view.get('.commit-choice').trigger('click'); await flushPromises();
  expect(view.get('.commit-files .file-status.added').text()).toBe('+');
  expect(view.get('.commit-files .file-status.deleted').text()).toBe('−');
  expect(view.get('.commit-files .file-status.modified').text()).toBe('M');
});

it.each([true, false])('cancels a pending diff when closing its accordion and ignores late completion (success=%s)', async success => {
  const api = mock(); const pending = deferred<CustomResponse<CommitDiff>>(); api.diff.mockReturnValueOnce(pending.promise);
  const view = render(); await flushPromises(); await view.get('.commit-choice').trigger('click'); await flushPromises();
  const toggle = view.get('.commit-file-toggle');
  await toggle.trigger('click');
  expect(view.get('.commit-file-diff').attributes('aria-busy')).toBe('true');
  await toggle.trigger('click');
  expect(api.diff.mock.calls[0]![4]!.aborted).toBe(true);
  if (success) pending.resolve(ok(patch)); else pending.reject(new Error('cancelled'));
  await flushPromises();
  expect(view.find('.commit-file-diff').exists()).toBe(false);
  expect(view.findComponent(DiffViewer).exists()).toBe(false);
  expect(view.find('[role="alert"]').exists()).toBe(false);
});

it('places references below the subject and renders round merge controls with colored lanes', async () => {
  const api = mock();
  const merge = { ...commit, sha: 'merge', subject: 'Merge feature', parents: ['main', 'side'] };
  api.list.mockResolvedValue(ok({ ...page, commits: [merge, { ...commit, sha: 'side', refs: '', parents: ['root'] }, { ...commit, sha: 'main', refs: '', parents: ['root'] }, { ...commit, sha: 'root', refs: '', parents: [] }] }));
  const view = render(); await flushPromises();
  const summary = view.get('.commit-summary');
  const strips = summary.findAllComponents(HorizontalScroll);
  expect(strips).toHaveLength(2);
  expect(strips[0]!.find('.commit-choice').exists()).toBe(true);
  expect(strips[1]!.find('.reference-pill').exists()).toBe(true);
  const svg = view.get('svg.commit-graph');
  expect(svg.attributes('viewBox')).toBeUndefined();
  expect(svg.attributes('preserveAspectRatio')).toBeUndefined();
  expect(svg.attributes('height')).toBe('56');
  expect(svg.get('circle').attributes('r')).toBe('7');
  expect(svg.findAll('path')[0]!.attributes('stroke')).not.toBe(svg.findAll('path')[1]!.attributes('stroke'));
  expect(view.findAll('svg.commit-graph')[1]!.get('circle').attributes('fill')).toBe(view.findAll('svg.commit-graph')[1]!.get('circle').attributes('stroke'));
  const toggle = view.get('.merge-toggle');
  expect(toggle.attributes('aria-expanded')).toBe('true');
  await view.findAll('.commit-choice')[1]!.trigger('click'); await flushPromises();
  await toggle.trigger('click'); await flushPromises();
  expect(toggle.attributes('aria-expanded')).toBe('false');
  expect(view.findAll('.commit-choice')).toHaveLength(3);
  expect(api.detail).toHaveBeenLastCalledWith(1, 2, 'merge', expect.any(AbortSignal));
  await toggle.trigger('click'); await flushPromises();
  expect(view.findAll('.commit-choice')).toHaveLength(4);
  expect(toggle.attributes('aria-expanded')).toBe('true');
  await toggle.trigger('click'); await flushPromises();
  await view.get('.history-refresh').trigger('click'); await flushPromises();
  expect(view.get('.merge-toggle').attributes('aria-expanded')).toBe('true');
  expect(view.findAll('.commit-choice')).toHaveLength(4);
});

it('applies existing merge folds to newly loaded pages without additional Git queries', async () => {
  const api = mock();
  const merge = { ...commit, sha: 'merge', parents: ['main', 'side'] };
  api.list.mockResolvedValueOnce(ok({ ...page, hasMore: true, commits: [merge] }))
    .mockResolvedValueOnce(ok({ ...page, commits: [{ ...commit, sha: 'side', parents: ['root'] }, { ...commit, sha: 'main', parents: ['root'] }, { ...commit, sha: 'root', parents: [] }] }));
  const view = render(); await flushPromises();
  await view.get('.merge-toggle').trigger('click');
  await view.get('.history-more').trigger('click'); await flushPromises();
  expect(view.findAll('.commit-choice')).toHaveLength(3);
  expect(api.list).toHaveBeenCalledTimes(2); expect(api.detail).not.toHaveBeenCalled();
  await view.get('.merge-toggle').trigger('click'); await flushPromises();
  expect(view.findAll('.commit-choice')).toHaveLength(4);
});
