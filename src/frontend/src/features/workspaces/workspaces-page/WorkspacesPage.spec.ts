import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import WorkspacesPage from './WorkspacesPage.vue';
import WorkspaceCreateForm from '../workspace-create-form/WorkspaceCreateForm.vue';
import WorkspaceActionDialog from '../workspace-action-dialog/WorkspaceActionDialog.vue';
import WorkspaceRepositoriesDialog from '../workspace-repositories-dialog/WorkspaceRepositoriesDialog.vue';
import { WorkspaceRepositoryApiService } from '../../../core/workspaces/workspace-repository-api.service';
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
      Dialog: { props: ['visible', 'header', 'closable'], emits: ['update:visible'], template: '<div v-if="visible"><h3>{{ header }}</h3><slot /></div>' },
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

  it('opens rename, preserves the entered name on conflict, and refreshes after success', async () => {
    const list = vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockResolvedValue(listing());
    const update = vi.spyOn(WorkspaceApiService.prototype, 'updateWorkspace')
      .mockRejectedValueOnce(new ApiError('Duplicate name')).mockResolvedValue(created);
    const page = render(); await flushPromises();
    await page.get('.workspace-row-actions button:first-child').trigger('click');
    let dialog = page.getComponent(WorkspaceActionDialog);
    expect(dialog.props('mode')).toBe('rename');
    await dialog.get('input').setValue('  New Team  ');
    await dialog.get('form').trigger('submit'); await flushPromises();
    expect(update).toHaveBeenCalledWith({ id: 1, name: 'New Team' }, expect.any(AbortSignal));
    expect(dialog.get('[role="alert"]').text()).toBe('Duplicate name');
    expect(dialog.get<HTMLInputElement>('input').element.value).toBe('  New Team  ');
    expect(page.find('.workspace-feedback').exists()).toBe(false);
    await dialog.get('form').trigger('submit'); await flushPromises();
    expect(page.findComponent(WorkspaceActionDialog).exists()).toBe(false);
    expect(list).toHaveBeenLastCalledWith(0, expect.any(AbortSignal));
    expect(page.get('[role="status"]').text()).toBe('Created');
  });

  it('cancels both actions without calling a mutation and resets the rename input on reopening', async () => {
    vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockResolvedValue(listing());
    const update = vi.spyOn(WorkspaceApiService.prototype, 'updateWorkspace');
    const remove = vi.spyOn(WorkspaceApiService.prototype, 'deleteWorkspace');
    const page = render(); await flushPromises();
    await page.get('.workspace-row-actions button:first-child').trigger('click');
    await page.getComponent(WorkspaceActionDialog).get('input').setValue('Discard');
    page.getComponent(WorkspaceActionDialog).vm.$emit('cancel'); await flushPromises();
    expect(page.findComponent(WorkspaceActionDialog).exists()).toBe(false);
    await page.get('.workspace-row-actions button:first-child').trigger('click');
    expect(page.getComponent(WorkspaceActionDialog).get<HTMLInputElement>('input').element.value).toBe('Team');
    page.getComponent(WorkspaceActionDialog).vm.$emit('cancel'); await flushPromises();
    await page.get('.workspace-row-actions button:last-child').trigger('click');
    expect(page.getComponent(WorkspaceActionDialog).props('mode')).toBe('delete');
    page.getComponent(WorkspaceActionDialog).vm.$emit('cancel'); await flushPromises();
    expect(update).not.toHaveBeenCalled();
    expect(remove).not.toHaveBeenCalled();
  });

  it('requires confirmation, blocks dismiss and duplicate deletes while saving, then returns to page zero', async () => {
    const list = vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces')
      .mockResolvedValueOnce(listing(1)).mockResolvedValue(empty);
    const pending = deferred<CustomResponse<number>>();
    const remove = vi.spyOn(WorkspaceApiService.prototype, 'deleteWorkspace').mockReturnValue(pending.promise);
    const page = render(); await flushPromises();
    await page.get('.workspace-row-actions button:last-child').trigger('click');
    const dialog = page.getComponent(WorkspaceActionDialog);
    expect(remove).not.toHaveBeenCalled();
    await dialog.get('form').trigger('submit'); await flushPromises();
    expect(remove).toHaveBeenCalledWith(1, expect.any(AbortSignal));
    expect(dialog.props('busy')).toBe(true);
    dialog.vm.$emit('cancel'); dialog.vm.$emit('delete'); await flushPromises();
    expect(page.findComponent(WorkspaceActionDialog).exists()).toBe(true);
    expect(remove).toHaveBeenCalledTimes(1);
    expect(page.get('.workspace-row-actions button:first-child').attributes('disabled')).toBeDefined();
    pending.resolve({ success: true, result: 1, messages: ['Deleted'], errors: [] }); await flushPromises();
    expect(page.findComponent(WorkspaceActionDialog).exists()).toBe(false);
    expect(page.text()).toContain('Você ainda não possui workspaces');
    expect(page.get('[role="status"]').text()).toBe('Deleted');
    expect(list).toHaveBeenLastCalledWith(0, expect.any(AbortSignal));
  });

  it.each([new ApiError('Workspace not found'), new Error('technical')])('keeps a failed deletion open for review or retry', async failure => {
    vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockResolvedValue(listing());
    vi.spyOn(WorkspaceApiService.prototype, 'deleteWorkspace').mockRejectedValue(failure);
    const page = render(); await flushPromises();
    await page.get('.workspace-row-actions button:last-child').trigger('click');
    const dialog = page.getComponent(WorkspaceActionDialog);
    await dialog.get('form').trigger('submit'); await flushPromises();
    expect(dialog.props('busy')).toBe(false);
    expect(dialog.get('[role="alert"]').text()).toBe(failure instanceof ApiError ? failure.message : 'Não foi possível carregar ou salvar os workspaces.');
    expect(page.get('li').text()).toContain('Team');
  });

  it.each(['rename', 'delete'] as const)('cancels an outstanding %s when leaving the page', async mode => {
    vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockResolvedValue(listing());
    const renameRequest = deferred<typeof created>();
    const deleteRequest = deferred<CustomResponse<number>>();
    const update = vi.spyOn(WorkspaceApiService.prototype, 'updateWorkspace').mockReturnValue(renameRequest.promise);
    const remove = vi.spyOn(WorkspaceApiService.prototype, 'deleteWorkspace').mockReturnValue(deleteRequest.promise);
    const page = render(); await flushPromises();
    await page.get(mode === 'rename' ? '.workspace-row-actions button:first-child' : '.workspace-row-actions button:last-child').trigger('click');
    await page.getComponent(WorkspaceActionDialog).get('form').trigger('submit'); await flushPromises();
    page.unmount(); wrapper = undefined;
    if (mode === 'rename') {
      expect(update.mock.calls[0]![1]!.aborted).toBe(true);
      renameRequest.resolve(created);
    } else {
      expect(remove.mock.calls[0]![1]!.aborted).toBe(true);
      deleteRequest.reject(new Error('cancelled'));
    }
    await flushPromises();
  });


  it('opens repository management for the selected workspace, refreshes counts and closes it', async () => {
    const list = vi.spyOn(WorkspaceApiService.prototype, 'getWorkspaces').mockResolvedValue(listing());
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue({ success: true, result: [], errors: [], messages: [] });
    const page = render(); await flushPromises();
    const manage = page.findAll('.workspace-row-actions button').find(button => button.text() === 'Repositórios')!;
    await manage.trigger('click'); await flushPromises();
    const dialog = page.getComponent(WorkspaceRepositoriesDialog);
    expect(dialog.props('workspace')).toEqual(workspace);
    dialog.vm.$emit('changed'); await flushPromises();
    expect(list).toHaveBeenCalledTimes(2);
    expect(page.findComponent(WorkspaceRepositoriesDialog).exists()).toBe(true);
    dialog.vm.$emit('close'); await flushPromises();
    expect(page.findComponent(WorkspaceRepositoriesDialog).exists()).toBe(false);
  });
});
