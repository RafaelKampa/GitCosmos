import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import WorkspacesPage from './WorkspacesPage.vue';
import WorkspaceCreateForm from '../workspace-create-form/WorkspaceCreateForm.vue';
import { WorkspaceApiService } from '../../../core/workspaces/workspace-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
import type { PagedResult } from '../../../shared/models/pagination.model';
import type { WorkspaceResponse } from '../../../core/workspaces/models/responses/workspace-response.model';

const workspace: WorkspaceResponse = { id: 1, name: 'Team', createdAt: '2026-11-20T00:00:00Z', repositoryCount: 0 };
type ListResponse = CustomResponse<PagedResult<WorkspaceResponse>>;
const empty: ListResponse = { success: true, errors: [], messages: [], result: { items: [], page: 0, pageSize: 10, totalRows: 0, totalPages: 0 } };
const created: CustomResponse<WorkspaceResponse> = { success: true, errors: [], messages: ['Created'], result: workspace };
function listing(page = 0): ListResponse {
  return { ...empty, result: { items: [workspace], page, pageSize: 10, totalRows: 11, totalPages: 2 } };
}
function deferred<T>() {
  let resolve!: (result: T) => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<T>((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}

describe('workspaces page', () => {
  let wrapper: VueWrapper | undefined;
  beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
  afterEach(() => { wrapper?.unmount(); wrapper = undefined; vi.unstubAllGlobals(); });
  function render() {
    wrapper = mount(WorkspacesPage, { global: { plugins: [PrimeVue], stubs: {
      Button: { props: ['label', 'disabled'], template: '<button :disabled="disabled">{{ label }}</button>' },
    } } });
    return wrapper;
  }
  function submit(page: VueWrapper, name = 'Team') {
    page.getComponent(WorkspaceCreateForm).vm.$emit('create', name);
  }

  it('uses the selected language for real API calls', async () => {
    useLanguageStore().setLanguage('en');
    const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify(empty)));
    vi.stubGlobal('fetch', fetchMock);
    const page = render(); await flushPromises();
    expect(page.text()).toContain('You have no workspaces yet');
    expect(fetchMock).toHaveBeenCalledWith('/api/Workspace/GetAllWorkspaces?page=0&pageSize=10',
      expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
  });
  it('shows loading, then a localized empty state', async () => {
    const pending = deferred<ListResponse>();
    vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockReturnValue(pending.promise);
    const page = render();
    expect(page.get('[role="status"]').text()).toContain('Carregando');
    expect(page.attributes('aria-busy')).toBe('true');
    pending.resolve(empty); await flushPromises();
    expect(page.text()).toContain('Você ainda não possui workspaces');
    expect(page.find('ul').exists()).toBe(false);
    expect(page.find('nav').exists()).toBe(false);
  });

  it('lists workspaces and navigates through zero-based pages', async () => {
    const request = vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces')
      .mockResolvedValueOnce(listing()).mockResolvedValueOnce(listing(1)).mockResolvedValueOnce(listing());
    const page = render(); await flushPromises();
    expect(page.get('h2').text()).toBe('Team');
    expect(page.get('li p').text()).toContain('0');
    expect(page.get('nav button:first-child').attributes('disabled')).toBeDefined();
    await page.get('nav button:last-child').trigger('click'); await flushPromises();
    expect(request.mock.calls[1]![0]).toBe(1);
    expect(page.get('nav span').text()).toBe('2 / 2');
    expect(page.get('nav button:last-child').attributes('disabled')).toBeDefined();
    await page.get('nav button:first-child').trigger('click'); await flushPromises();
    expect(request.mock.calls[2]![0]).toBe(0);
  });

  it.each([new ApiError('Localized failure'), new Error('technical')])('shows failures and allows retry', async failure => {
    vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockRejectedValueOnce(failure).mockResolvedValue(empty);
    const page = render(); await flushPromises();
    expect(page.get('[role="alert"]').text()).toBe(failure instanceof ApiError ? failure.message : 'Não foi possível carregar ou salvar os workspaces.');
    await page.get('.workspace-feedback button').trigger('click'); await flushPromises();
    expect(page.find('[role="alert"]').exists()).toBe(false);
  });

  it('rejects an incomplete successful response', async () => {
    vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockResolvedValue({ ...empty, result: null });
    const page = render(); await flushPromises();
    expect(page.get('[role="alert"]').text()).toContain('Não foi possível');
  });

  it('creates once, prevents duplicate submissions, resets the form and refreshes page zero', async () => {
    const pending = deferred<typeof created>();
    const list = vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockResolvedValue(listing(1));
    const save = vi.spyOn(WorkspaceApiService.prototype, 'createWorkspace').mockReturnValue(pending.promise);
    const page = render(); await flushPromises();
    await page.get('input').setValue('Team');
    submit(page); submit(page);
    await flushPromises();
    expect(save).toHaveBeenCalledTimes(1);
    expect(page.get('input').attributes('disabled')).toBeDefined();
    expect(page.get('nav button:first-child').attributes('disabled')).toBeDefined();
    pending.resolve(created); await flushPromises();
    expect(page.get('[role="status"]').text()).toBe('Created');
    expect(page.get<HTMLInputElement>('input').element.value).toBe('');
    expect(list).toHaveBeenLastCalledWith(0, expect.any(AbortSignal));
    expect(page.attributes('aria-busy')).toBe('false');
  });

  it.each([new ApiError('Name already exists'), new Error('technical')])('preserves the name after a failed save', async failure => {
    vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockResolvedValue(empty);
    vi.spyOn(WorkspaceApiService.prototype, 'createWorkspace').mockRejectedValue(failure);
    const page = render(); await flushPromises();
    await page.get('input').setValue('Team');
    await page.get('form').trigger('submit'); await flushPromises();
    expect(page.get('[role="alert"]').text()).toBe(failure instanceof ApiError ? failure.message : 'Não foi possível carregar ou salvar os workspaces.');
    expect(page.get<HTMLInputElement>('input').element.value).toBe('Team');
    expect(page.get('input').attributes('disabled')).toBeUndefined();
  });

  it('changes language, cancels stale lists and clears success text', async () => {
    const stale = deferred<ListResponse>();
    const list = vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockReturnValueOnce(stale.promise).mockResolvedValue(empty);
    vi.spyOn(WorkspaceApiService.prototype, 'createWorkspace').mockResolvedValue(created);
    const page = render();
    useLanguageStore().setLanguage('en'); await flushPromises();
    expect(list.mock.calls[0]![1]!.aborted).toBe(true);
    stale.resolve(listing()); await flushPromises();
    expect(page.find('li').exists()).toBe(false);
    submit(page); await flushPromises();
    expect(page.text()).toContain('Created');
    useLanguageStore().setLanguage('es'); await flushPromises();
    expect(page.text()).not.toContain('Created');
    expect(page.text()).toContain('Aún no tiene workspaces');
  });

  it.each([true, false])('ignores late list results after unmount (success=%s)', async succeeds => {
    const pending = deferred<ListResponse>();
    const list = vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockReturnValue(pending.promise);
    const page = render(); page.unmount(); wrapper = undefined;
    expect(list.mock.calls[0]![1]!.aborted).toBe(true);
    if (succeeds) pending.resolve(empty); else pending.reject(new Error('cancelled'));
    await flushPromises();
  });

  it.each([true, false])('ignores late saves after unmount (success=%s)', async succeeds => {
    vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockResolvedValue(empty);
    const pending = deferred<typeof created>();
    const save = vi.spyOn(WorkspaceApiService.prototype, 'createWorkspace').mockReturnValue(pending.promise);
    const page = render(); await flushPromises();
    submit(page); await flushPromises();
    page.unmount(); wrapper = undefined;
    expect(save.mock.calls[0]![1]!.aborted).toBe(true);
    if (succeeds) pending.resolve(created); else pending.reject(new Error('cancelled'));
    await flushPromises();
  });
});
