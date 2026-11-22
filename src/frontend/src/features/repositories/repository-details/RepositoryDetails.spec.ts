import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import RepositoryDetails from './RepositoryDetails.vue';
import FileChangesList from '../file-changes-list/FileChangesList.vue';
import DiffViewer from '../diff-viewer/DiffViewer.vue';
import { RepositoryChangesApiService } from '../../../core/repositories/repository-changes-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
import type { RepositoryChanges, FileDiff, DiffSelection } from '../../../core/repositories/models/repository-changes.model';

const changes: CustomResponse<RepositoryChanges> = { success: true, errors: [], messages: [], result: { id: 2, name: 'Repo', localPath: 'C:/repo',
  status: { branch: 'main', isDetached: false, hasCommits: true, changedFiles: 1, modified: 1, added: 0, deleted: 0, renamed: 0, untracked: 0, conflicted: 0, staged: 1, unstaged: 1 },
  files: [{ path: 'file.txt', originalPath: null, indexStatus: 'M', workTreeStatus: 'M', isUntracked: false, isConflicted: false, isSubmodule: false }],
} };
const selected: DiffSelection = { path: 'file.txt', area: 'working' };
const patch: CustomResponse<FileDiff> = { success: true, errors: [], messages: [], result: { ...selected, kind: 'text', patch: '@@ -1 +1 @@\n-old\n+new\n' } };
function deferred<T>() {
  let resolve!: (value: T) => void; let reject!: (error: Error) => void;
  const promise = new Promise<T>((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}

describe('repository details', () => {
  let wrapper: VueWrapper | undefined;
  beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
  afterEach(() => { wrapper?.unmount(); wrapper = undefined; vi.unstubAllGlobals(); });
  function render() {
    wrapper = mount(RepositoryDetails, { props: { workspaceId: 1, repositoryId: 2 }, global: { plugins: [PrimeVue], stubs: {
      RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
      Button: { props: ['label', 'disabled'], template: '<button :disabled="disabled">{{ label }}</button>' },
    } } });
    return wrapper;
  }
  async function select(page: VueWrapper, selection = selected) {
    page.getComponent(FileChangesList).vm.$emit('select', selection); await flushPromises();
  }

  it('opens the linked repository, loads a selected diff and resets selection on refresh', async () => {
    const pending = deferred<typeof changes>();
    const list = vi.spyOn(RepositoryChangesApiService.prototype, 'getChanges').mockReturnValueOnce(pending.promise).mockResolvedValue(changes);
    const diff = vi.spyOn(RepositoryChangesApiService.prototype, 'getDiff').mockResolvedValue(patch);
    const page = render();
    expect(page.get('[role="status"]').text()).toContain('Consultando');
    expect(page.get('.repository-heading button').attributes('disabled')).toBeDefined();
    expect(page.get('a').attributes('href')).toBe('/workspaces/1');
    pending.resolve(changes); await flushPromises();
    expect(page.get('h1').text()).toBe('Repo'); expect(page.text()).toContain('main');
    expect(page.text()).toContain('Selecione um arquivo'); expect(diff).not.toHaveBeenCalled();
    await select(page, { ...selected, area: 'staged' });
    expect(diff).toHaveBeenLastCalledWith(1, 2, { path: 'file.txt', area: 'staged' }, expect.any(AbortSignal));
    expect(page.get('.file-diff-heading').text()).toContain('Preparadas');
    expect(page.getComponent(DiffViewer).props('diff')).toEqual(patch.result);
    await page.get('.repository-heading button').trigger('click'); await flushPromises();
    expect(list).toHaveBeenCalledTimes(2); expect(page.findComponent(DiffViewer).exists()).toBe(false);
  });

  it('shows clean, unborn and detached repository states', async () => {
    vi.spyOn(RepositoryChangesApiService.prototype, 'getChanges').mockResolvedValue({ ...changes, result: { ...changes.result!, files: [], status: { ...changes.result!.status, hasCommits: false, isDetached: true } } });
    const page = render(); await flushPromises();
    expect(page.text()).toContain('HEAD destacado'); expect(page.text()).toContain('ainda não possui commits');
    expect(page.get('.repository-clean').text()).toContain('Nenhuma alteração local');
    expect(page.findComponent(FileChangesList).exists()).toBe(false);
  });

  it.each([new ApiError('Vínculo não encontrado'), new Error('private'), null])('shows list failures and retries: %s', async cause => {
    const api = vi.spyOn(RepositoryChangesApiService.prototype, 'getChanges');
    if (cause) api.mockRejectedValueOnce(cause); else api.mockResolvedValueOnce({ ...changes, result: null });
    api.mockResolvedValue(changes);
    const page = render(); await flushPromises();
    expect(page.get('[role="alert"]').text()).toBe(cause instanceof ApiError ? cause.message : 'Não foi possível consultar as alterações do repositório.');
    await page.get('.repository-heading button').trigger('click'); await flushPromises();
    expect(page.find('[role="alert"]').exists()).toBe(false);
  });

  it.each([new ApiError('Arquivo mudou'), new Error('private'), null])('preserves selection after a diff error and allows retry: %s', async cause => {
    vi.spyOn(RepositoryChangesApiService.prototype, 'getChanges').mockResolvedValue(changes);
    const api = vi.spyOn(RepositoryChangesApiService.prototype, 'getDiff');
    if (cause) api.mockRejectedValueOnce(cause); else api.mockResolvedValueOnce({ ...patch, result: null });
    api.mockResolvedValue(patch);
    const page = render(); await flushPromises(); await select(page);
    expect(page.get('[role="alert"]').text()).toBe(cause instanceof ApiError ? cause.message : 'Não foi possível consultar as alterações do repositório.');
    expect(page.get('.file-diff-heading').text()).toContain('file.txt');
    expect(page.get('.file-diff-heading').text()).toContain('Não preparadas');
    await page.get('.diff-feedback button').trigger('click'); await flushPromises();
    expect(page.find('[role="alert"]').exists()).toBe(false);
    expect(page.findComponent(DiffViewer).exists()).toBe(true);
  });

  it('cancels a stale diff when a different file is selected', async () => {
    vi.spyOn(RepositoryChangesApiService.prototype, 'getChanges').mockResolvedValue(changes);
    const stale = deferred<typeof patch>();
    const api = vi.spyOn(RepositoryChangesApiService.prototype, 'getDiff').mockReturnValueOnce(stale.promise).mockResolvedValue(patch);
    const page = render(); await flushPromises();
    await select(page);
    expect(page.get('.file-diff-panel[aria-busy="true"]').text()).toContain('Carregando diferenças');
    await select(page, { path: 'other', area: 'staged' });
    expect(api.mock.calls[0]![3]!.aborted).toBe(true);
    stale.resolve({ ...patch, result: { ...patch.result!, patch: '+stale' } }); await flushPromises();
    expect(page.text()).not.toContain('+stale');
  });

  it('uses the selected language in actual HTTP requests and reloads on language change', async () => {
    const fetchMock = vi.fn().mockImplementation(async (url: string) => new Response(JSON.stringify(url.includes('GetDiff') ? patch : changes)));
    vi.stubGlobal('fetch', fetchMock); useLanguageStore().setLanguage('es');
    const page = render(); await flushPromises(); await select(page);
    expect(fetchMock).toHaveBeenLastCalledWith(expect.stringContaining('GetDiff'), expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
    useLanguageStore().setLanguage('en'); await flushPromises();
    expect(fetchMock).toHaveBeenLastCalledWith(expect.stringContaining('GetChanges'), expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
    expect(page.findComponent(DiffViewer).exists()).toBe(false);
  });

  it.each([true, false])('ignores stale list completion after changing repository: success=%s', async success => {
    const stale = deferred<typeof changes>();
    const api = vi.spyOn(RepositoryChangesApiService.prototype, 'getChanges').mockReturnValueOnce(stale.promise).mockResolvedValue(changes);
    const page = render(); await page.setProps({ workspaceId: 3, repositoryId: 4 }); await flushPromises();
    expect(api.mock.calls[0]![2]!.aborted).toBe(true); expect(api).toHaveBeenLastCalledWith(3, 4, expect.any(AbortSignal));
    if (success) stale.resolve({ ...changes, result: { ...changes.result!, name: 'stale' } }); else stale.reject(new Error('cancelled'));
    await flushPromises(); expect(page.get('h1').text()).toBe('Repo');
  });

  it.each([true, false])('aborts pending diffs when leaving the repository: success=%s', async success => {
    vi.spyOn(RepositoryChangesApiService.prototype, 'getChanges').mockResolvedValue(changes);
    const pending = deferred<typeof patch>();
    const api = vi.spyOn(RepositoryChangesApiService.prototype, 'getDiff').mockReturnValue(pending.promise);
    const page = render(); await flushPromises(); await select(page);
    page.unmount(); wrapper = undefined;
    expect(api.mock.calls[0]![3]!.aborted).toBe(true);
    if (success) pending.resolve(patch); else pending.reject(new Error('cancelled'));
    await flushPromises();
  });
});
