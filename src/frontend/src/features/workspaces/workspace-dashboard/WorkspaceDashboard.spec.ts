import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import WorkspaceDashboard from './WorkspaceDashboard.vue';
import RepositoryStatusCard from '../repository-status-card/RepositoryStatusCard.vue';
import { WorkspaceDashboardApiService } from '../../../core/workspaces/workspace-dashboard-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
import type { WorkspaceDashboardResponse } from '../../../core/workspaces/models/responses/workspace-dashboard-response.model';

type Response = CustomResponse<WorkspaceDashboardResponse>;
const empty: Response = { success: true, errors: [], messages: [], result: { workspaceId: 1, name: 'Team', checkedAt: '2026-11-22T12:00:00Z', repositories: [] } };
const populated: Response = { ...empty, result: { ...empty.result!, repositories: [{ id: 1, name: 'repo', localPath: 'C:/repo', state: 'clean', error: null, status: {
  branch: 'main', isDetached: false, hasCommits: true, changedFiles: 0, modified: 0, added: 0, deleted: 0, renamed: 0, untracked: 0, conflicted: 0, staged: 0, unstaged: 0,
} }, { id: 2, name: 'missing', localPath: 'C:/missing', state: 'unavailable', error: 'Pasta indisponível', status: null }] } };
function deferred() {
  let resolve!: (value: Response) => void; let reject!: (reason: Error) => void;
  const promise = new Promise<Response>((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}

describe('workspace dashboard', () => {
  let wrapper: VueWrapper | undefined;
  beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
  afterEach(() => { wrapper?.unmount(); wrapper = undefined; vi.unstubAllGlobals(); });
  function render() {
    wrapper = mount(WorkspaceDashboard, { props: { workspaceId: 1 }, global: { plugins: [PrimeVue], stubs: {
      RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
      Button: { props: ['label', 'disabled'], template: '<button :disabled="disabled">{{ label }}</button>' },
    } } });
    return wrapper;
  }

  it('shows loading, the workspace name, independent repository states and refresh', async () => {
    const pending = deferred();
    const api = vi.spyOn(WorkspaceDashboardApiService.prototype, 'getDashboard').mockReturnValueOnce(pending.promise).mockResolvedValue(empty);
    const page = render();
    expect(page.get('[role="status"]').text()).toContain('Consultando');
    expect(page.get('button').attributes('disabled')).toBeDefined();
    expect(page.get('a').attributes('href')).toBe('/workspaces');
    pending.resolve(populated); await flushPromises();
    expect(page.get('h1').text()).toContain('Team');
    expect(page.findAllComponents(RepositoryStatusCard)).toHaveLength(2);
    expect(page.text()).toContain('Pasta indisponível');
    expect(page.text()).toContain('As contagens podem se sobrepor');
    await page.get('button').trigger('click'); await flushPromises();
    expect(api).toHaveBeenCalledTimes(2);
    expect(page.findAllComponents(RepositoryStatusCard)).toHaveLength(0);
    expect(page.text()).toContain('ainda não possui repositórios');
  });

  it.each([new ApiError('Workspace não encontrado'), new Error('private'), null])('shows API, network and incomplete response errors and supports retry: %s', async cause => {
    const api = vi.spyOn(WorkspaceDashboardApiService.prototype, 'getDashboard');
    if (cause) api.mockRejectedValueOnce(cause); else api.mockResolvedValueOnce({ ...empty, result: null });
    api.mockResolvedValue(empty);
    const page = render(); await flushPromises();
    expect(page.get('[role="alert"]').text()).toBe(cause instanceof ApiError ? cause.message : 'Não foi possível carregar o dashboard do workspace.');
    expect(page.text()).not.toContain('ainda não possui repositórios');
    await page.get('button').trigger('click'); await flushPromises();
    expect(page.find('[role="alert"]').exists()).toBe(false);
  });

  it('uses the selected language in real HTTP requests', async () => {
    useLanguageStore().setLanguage('es');
    const fetchMock = vi.fn().mockImplementation(async () => new globalThis.Response(JSON.stringify(empty)));
    vi.stubGlobal('fetch', fetchMock);
    const page = render(); await flushPromises();
    expect(fetchMock).toHaveBeenCalledWith('/api/WorkspaceDashboard/GetDashboard?workspaceId=1', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
    expect(page.get('h1').text()).toContain('Dashboard del workspace');
    useLanguageStore().setLanguage('en'); await flushPromises();
    expect(fetchMock).toHaveBeenLastCalledWith('/api/WorkspaceDashboard/GetDashboard?workspaceId=1', expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
  });

  it('aborts stale requests after a workspace change and ignores their results', async () => {
    const stale = deferred();
    const api = vi.spyOn(WorkspaceDashboardApiService.prototype, 'getDashboard').mockReturnValueOnce(stale.promise).mockResolvedValue(empty);
    const page = render();
    await page.setProps({ workspaceId: 2 }); await flushPromises();
    expect(api.mock.calls[0]![1]!.aborted).toBe(true);
    expect(api).toHaveBeenLastCalledWith(2, expect.any(AbortSignal));
    stale.resolve(populated); await flushPromises();
    expect(page.findAllComponents(RepositoryStatusCard)).toHaveLength(0);
  });

  it.each([true, false])('aborts on unmount and ignores late completion: success=%s', async success => {
    const pending = deferred();
    const api = vi.spyOn(WorkspaceDashboardApiService.prototype, 'getDashboard').mockReturnValue(pending.promise);
    const page = render(); page.unmount(); wrapper = undefined;
    expect(api.mock.calls[0]![1]!.aborted).toBe(true);
    if (success) pending.resolve(empty); else pending.reject(new Error('cancelled'));
    await flushPromises();
  });
});
