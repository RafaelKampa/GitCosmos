import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import Dialog from 'primevue/dialog';
import Button from 'primevue/button';
import WorkspaceRepositoriesDialog from './WorkspaceRepositoriesDialog.vue';
import FolderPathField from '../../../shared/components/folder-path-field/FolderPathField.vue';
import RepositoryCloneForm from '../repository-clone-form/RepositoryCloneForm.vue';
import { WorkspaceRepositoryApiService } from '../../../core/workspaces/workspace-repository-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
import type { RepositoryImportResponse } from '../../../core/workspaces/models/responses/repository-import-response.model';
import type { WorkspaceRepositoryResponse } from '../../../core/workspaces/models/responses/workspace-repository-response.model';

const workspace = { id: 3, name: 'Team', createdAt: '2026-11-22T00:00:00Z', repositoryCount: 0 };
const repository = { id: 8, name: 'repo', localPath: 'C:\\repo' };
type ListResponse = CustomResponse<readonly WorkspaceRepositoryResponse[]>;
const empty: ListResponse = { success: true, result: [], messages: [], errors: [] };
const listed: ListResponse = { ...empty, result: [repository] };
const added: CustomResponse<RepositoryImportResponse> = { success: true, result: { items: [{ localPath: repository.localPath, status: 'added', message: 'Added' }] }, messages: ['Added'], errors: [] };
const removed: CustomResponse<number> = { success: true, result: 8, messages: ['Removed'], errors: [] };
function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<T>((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}
const dialogStub = {
  props: { visible: Boolean, header: String, modal: Boolean, closable: Boolean, closeOnEscape: Boolean, dismissableMask: Boolean },
  emits: ['update:visible'],
  template: '<div v-if="visible"><h2>{{ header }}</h2><button data-dialog-close :disabled="!closable" @click="$emit(\'update:visible\', false)">Close dialog</button><slot /></div>',
};

describe('workspace repositories', () => {
  let wrapper: VueWrapper | undefined;
  beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
  afterEach(() => { wrapper?.unmount(); wrapper = undefined; vi.unstubAllGlobals(); });
  function render() {
    wrapper = mount(WorkspaceRepositoriesDialog, { props: { workspace }, global: { plugins: [PrimeVue], stubs: {
      Dialog: dialogStub,
      Button: { props: ['label', 'disabled'], template: '<button :disabled="disabled">{{ label }}</button>' },
    } } });
    return wrapper;
  }

  it('loads real API data with the selected language and shows a localized empty state', async () => {
    useLanguageStore().setLanguage('en');
    const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify(empty)));
    vi.stubGlobal('fetch', fetchMock);
    const page = render(); await flushPromises();
    expect(fetchMock).toHaveBeenCalledWith('/api/WorkspaceRepository/GetRepositories?workspaceId=3',
      expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
    expect(page.text()).toContain('This workspace has no repositories yet');
    expect(page.get('h2').text()).toContain('Repositories — Team');
    expect(page.get('label').attributes('for')).toBe(page.get('input').attributes('id'));
    expect(page.get('input').attributes('maxlength')).toBe('32767');
    expect(page.get('input').attributes('required')).toBeDefined();
  });

  it('shows loading, lists the name and path, and closes without a mutation', async () => {
    const pending = deferred<ListResponse>();
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockReturnValue(pending.promise);
    const page = render();
    expect(page.get('[role="status"]').text()).toContain('Carregando');
    expect(page.get('input').attributes('disabled')).toBeDefined();
    pending.resolve(listed); await flushPromises();
    expect(page.get('li h3').text()).toBe('repo');
    expect(page.get('li code').text()).toBe(repository.localPath);
    await page.get('[data-dialog-close]').trigger('click');
    expect(page.emitted('close')).toHaveLength(1);
    const close = page.findAll('button').find(button => button.text() === 'Fechar')!;
    await close.trigger('click');
    expect(page.emitted('close')).toHaveLength(2);
  });

  it.each([new ApiError('Workspace missing'), new Error('technical')])('shows a list error without a misleading empty state and supports retry', async failure => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockRejectedValueOnce(failure).mockResolvedValue(empty);
    const page = render(); await flushPromises();
    expect(page.get('[role="alert"]').text()).toBe(failure instanceof ApiError ? failure.message : 'Não foi possível concluir a operação com os repositórios.');
    expect(page.text()).not.toContain('Este workspace ainda não possui repositórios');
    await page.get('.repository-retry').trigger('click'); await flushPromises();
    expect(page.find('[role="alert"]').exists()).toBe(false);
    expect(page.text()).toContain('Este workspace ainda não possui repositórios');
  });

  it('rejects an incomplete successful list', async () => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue({ ...empty, result: null });
    const page = render(); await flushPromises();
    expect(page.get('[role="alert"]').text()).toContain('Não foi possível');
  });

  it('adds a trimmed path once, blocks close while saving, preserves the selected path and reports changed counts', async () => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValueOnce(empty).mockResolvedValue(listed);
    const pending = deferred<typeof added>();
    const save = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories').mockReturnValue(pending.promise);
    const page = render(); await flushPromises();
    await page.get('input').setValue('  C:\\repo  ');
    await page.get('form').trigger('submit'); await page.get('form').trigger('submit'); await flushPromises();
    expect(save).toHaveBeenCalledTimes(1);
    expect(save).toHaveBeenCalledWith({ workspaceId: 3, localPath: 'C:\\repo' }, expect.any(AbortSignal));
    expect(page.getComponent(Dialog).props()).toMatchObject({ modal: true, closable: false, closeOnEscape: false, dismissableMask: false });
    page.getComponent(Dialog).vm.$emit('update:visible', false);
    const close = page.findAllComponents(Button).find(button => button.props('label') === 'Fechar')!;
    close.vm.$emit('click'); await flushPromises();
    expect(page.emitted('close')).toBeUndefined();
    pending.resolve(added); await flushPromises();
    expect(page.get<HTMLInputElement>('input').element.value).toBe('  C:\\repo  ');
    expect(page.emitted('changed')).toHaveLength(1);
    expect(page.get('[role="status"]').text()).toBe('Added');
    expect(page.get('li').text()).toContain('repo');
  });

  it.each([new ApiError('Invalid repository'), new Error('technical')])('preserves the input and current list after a failed addition', async failure => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(listed);
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories').mockRejectedValue(failure);
    const page = render(); await flushPromises();
    await page.get('input').setValue('C:\\bad');
    await page.get('form').trigger('submit'); await flushPromises();
    expect(page.get<HTMLInputElement>('input').element.value).toBe('C:\\bad');
    expect(page.get('li').text()).toContain('repo');
    expect(page.get('[role="alert"]').text()).toBe(failure instanceof ApiError ? failure.message : 'Não foi possível concluir a operação com os repositórios.');
    expect(page.emitted('changed')).toBeUndefined();
  });

  it('allows canceling link removal before any request', async () => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(listed);
    const remove = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'removeRepository');
    const page = render(); await flushPromises();
    await page.get('li button').trigger('click');
    expect(page.get('.repository-removal').text()).toContain('arquivos e os vínculos em outros workspaces serão preservados');
    expect(page.get('input').attributes('disabled')).toBeDefined();
    await page.get('.repository-removal button:first-child').trigger('click');
    expect(page.find('.repository-removal').exists()).toBe(false);
    expect(remove).not.toHaveBeenCalled();
  });

  it('confirms removal, blocks cancel while saving and refreshes the list after unlinking', async () => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValueOnce(listed).mockResolvedValue(empty);
    const pending = deferred<typeof removed>();
    const remove = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'removeRepository').mockReturnValue(pending.promise);
    const page = render(); await flushPromises();
    await page.get('li button').trigger('click');
    expect(remove).not.toHaveBeenCalled();
    await page.get('.repository-removal .workspace-danger-action').trigger('click'); await flushPromises();
    expect(remove).toHaveBeenCalledWith(3, 8, expect.any(AbortSignal));
    const cancel = page.findAllComponents(Button).find(button => button.props('label') === 'Cancelar')!;
    cancel.vm.$emit('click'); await flushPromises();
    expect(page.find('.repository-removal').exists()).toBe(true);
    pending.resolve(removed); await flushPromises();
    expect(page.find('.repository-removal').exists()).toBe(false);
    expect(page.get('[role="status"]').text()).toBe('Removed');
    expect(page.emitted('changed')).toHaveLength(1);
    expect(page.find('li').exists()).toBe(false);
  });

  it('keeps the confirmation and files explanation visible if unlinking fails', async () => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(listed);
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'removeRepository').mockRejectedValue(new ApiError('Link not found'));
    const page = render(); await flushPromises();
    await page.get('li button').trigger('click');
    await page.get('.repository-removal .workspace-danger-action').trigger('click'); await flushPromises();
    expect(page.get('[role="alert"]').text()).toBe('Link not found');
    expect(page.find('.repository-removal').exists()).toBe(true);
    expect(page.find('li').exists()).toBe(true);
  });

  it('reports success separately when refreshing after an addition fails', async () => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValueOnce(empty).mockRejectedValue(new ApiError('Refresh failed'));
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories').mockResolvedValue(added);
    const page = render(); await flushPromises();
    await page.get('input').setValue('C:\\repo');
    await page.get('form').trigger('submit'); await flushPromises();
    expect(page.get('[role="status"]').text()).toBe('Added');
    expect(page.get('[role="alert"]').text()).toBe('Refresh failed');
    expect(page.emitted('changed')).toHaveLength(1);
  });

  it('changes language and ignores stale list responses', async () => {
    const stale = deferred<ListResponse>();
    const request = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockReturnValueOnce(stale.promise).mockResolvedValue(empty);
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories').mockResolvedValue(added);
    const page = render();
    useLanguageStore().setLanguage('en'); await flushPromises();
    expect(request.mock.calls[0]![1]!.aborted).toBe(true);
    stale.resolve(listed); await flushPromises();
    expect(page.find('li').exists()).toBe(false);
    await page.get('input').setValue('C:\\repo'); await page.get('form').trigger('submit'); await flushPromises();
    expect(page.text()).toContain('Added');
    useLanguageStore().setLanguage('es'); await flushPromises();
    expect(page.text()).not.toContain('Added');
    expect(page.text()).toContain('Este workspace aún no tiene repositorios');
  });

  it.each([true, false])('ignores list completion after unmount (success=%s)', async succeeds => {
    const pending = deferred<ListResponse>();
    const request = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockReturnValue(pending.promise);
    const page = render(); page.unmount(); wrapper = undefined;
    expect(request.mock.calls[0]![1]!.aborted).toBe(true);
    if (succeeds) pending.resolve(empty); else pending.reject(new Error('cancelled'));
    await flushPromises();
  });

  it.each([true, false])('ignores mutations after unmount (success=%s)', async succeeds => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(empty);
    const pending = deferred<typeof added>();
    const request = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories').mockReturnValue(pending.promise);
    const page = render(); await flushPromises();
    await page.get('input').setValue('C:\\repo'); await page.get('form').trigger('submit'); await flushPromises();
    page.unmount(); wrapper = undefined;
    expect(request.mock.calls[0]![1]!.aborted).toBe(true);
    if (succeeds) pending.resolve(added); else pending.reject(new Error('cancelled'));
    await flushPromises();
  });

  it('uses the selected native folder without adding automatically and blocks closing while selecting', async () => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(empty);
    const add = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories').mockResolvedValue(added);
    const page = render(); await flushPromises();
    const field = page.getComponent(FolderPathField);
    field.vm.$emit('busy', true); await flushPromises();
    page.getComponent(Dialog).vm.$emit('update:visible', false);
    await page.get('form').trigger('submit'); await flushPromises();
    expect(page.emitted('close')).toBeUndefined();
    expect(add).not.toHaveBeenCalled();
    field.vm.$emit('update:modelValue', 'C:\\selected');
    field.vm.$emit('error', 'Picker diagnostic'); field.vm.$emit('busy', false); await flushPromises();
    expect(page.get<HTMLInputElement>('input').element.value).toBe('C:\\selected');
    expect(page.get('[role="alert"]').text()).toBe('Picker diagnostic');
    await page.get('form').trigger('submit'); await flushPromises();
    expect(add).toHaveBeenCalledWith({ workspaceId: 3, localPath: 'C:\\selected' }, expect.any(AbortSignal));
  });

  it('reports immediate child additions, duplicates, ignored folders and failures while preserving the parent path', async () => {
    const list = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(empty);
    const scan = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories').mockResolvedValue({
      success: true, errors: [], messages: ['2 adicionados, 1 já vinculado, 1 ignorado, 1 com falha'], result: { items: [
        { localPath: 'C:/Repos/ReposTestes/GitCosmosTest1', status: 'added', message: 'Adicionado 1' },
        { localPath: 'C:/Repos/ReposTestes/GitCosmosTest2', status: 'added', message: 'Adicionado 2' },
        { localPath: 'C:/Repos/ReposTestes/AlreadyLinked', status: 'duplicate', message: 'Existente' },
        { localPath: 'C:/Repos/ReposTestes/OutraPasta', status: 'ignored', message: 'Sem Git' },
        { localPath: 'C:/Repos/ReposTestes/Failed', status: 'failed', message: 'Acesso indisponível' },
      ] },
    });
    const page = render(); await flushPromises();
    expect(page.find('input[type="checkbox"]').exists()).toBe(false);
    expect(page.get('#repository-path-help').text()).toContain('primeiro nível');
    page.getComponent(FolderPathField).vm.$emit('update:modelValue', ' C:/Repos/ReposTestes ');
    await flushPromises();
    await page.get('form').trigger('submit'); await flushPromises();
    expect(scan).toHaveBeenCalledWith({ workspaceId: 3, localPath: 'C:/Repos/ReposTestes' }, expect.any(AbortSignal));
    expect(page.get('.repository-import-results').text()).toContain('OutraPasta');
    expect(page.findAll('.repository-import-results li')).toHaveLength(5);
    expect(page.get('.repository-import-results').text()).toContain('Já vinculado');
    expect(page.get('.repository-import-results').text()).toContain('Ignorado');
    expect(page.get('.repository-import-results').text()).toContain('Falha');
    expect(page.get('#repository-path').element).toHaveProperty('value', ' C:/Repos/ReposTestes ');
    expect(page.emitted('changed')).toHaveLength(1);
    expect(list).toHaveBeenCalledTimes(2);
    useLanguageStore().setLanguage('en'); await flushPromises();
    expect(page.find('.repository-import-results').exists()).toBe(false);
  });

  it('reports an empty parent and clears the result when switching to clone', async () => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(empty);
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories').mockResolvedValue({ ...empty, result: { items: [] } });
    const page = render(); await flushPromises();
    await page.get('#repository-path').setValue('C:/Repos');
    await page.get('form').trigger('submit'); await flushPromises();
    expect(page.get('.repository-import-results').text()).toContain('Nenhuma subpasta foi encontrada');
    await page.get('.repository-add-options button:last-child').trigger('click');
    expect(page.find('.repository-import-results').exists()).toBe(false);
  });

  it.each([new ApiError('Pasta sem acesso'), new Error('private'), null])('preserves the parent path on import failure or incomplete response: %s', async failure => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(empty);
    const scan = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories');
    if (failure) scan.mockRejectedValue(failure); else scan.mockResolvedValue({ ...empty, result: null });
    const page = render(); await flushPromises();
    await page.get('#repository-path').setValue('C:/Repos');
    await page.get('form').trigger('submit'); await flushPromises();
    expect(page.get('[role="alert"]').text()).toBe(failure instanceof ApiError ? failure.message : 'Não foi possível concluir a operação com os repositórios.');
    expect(page.get('#repository-path').element).toHaveProperty('value', 'C:/Repos');
    expect(page.emitted('changed')).toBeUndefined();
  });

  it('blocks duplicate imports and discards their late response after unmount', async () => {
    vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(empty);
    const pending = deferred<CustomResponse<import('../../../core/workspaces/models/responses/repository-import-response.model').RepositoryImportResponse>>();
    const scan = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'importRepositories').mockReturnValue(pending.promise);
    const page = render(); await flushPromises();
    await page.get('#repository-path').setValue('C:/Repos');
    await page.get('form').trigger('submit');
    await page.get('form').trigger('submit');
    expect(scan).toHaveBeenCalledTimes(1);
    expect(page.get('#repository-path').attributes('disabled')).toBeDefined();
    const signal = scan.mock.calls[0]![1]!;
    page.unmount(); wrapper = undefined;
    expect(signal.aborted).toBe(true);
    pending.resolve({ ...empty, result: { items: [] } }); await flushPromises();
    expect(page.emitted('changed')).toBeUndefined();
  });

  it('offers local and clone options, locks the outer dialog during cloning, and refreshes after a clone', async () => {
    const list = vi.spyOn(WorkspaceRepositoryApiService.prototype, 'getRepositories').mockResolvedValue(empty);
    const page = render(); await flushPromises();
    expect(page.get('.repository-add-options button:first-child').text()).toBe('Adicionar repositório local');
    await page.get('.repository-add-options button:last-child').trigger('click');
    const form = page.getComponent(RepositoryCloneForm);
    expect(form.props('workspaceId')).toBe(3);
    form.vm.$emit('busy', true); await flushPromises();
    expect(page.getComponent(Dialog).props('closable')).toBe(false);
    page.getComponent(Dialog).vm.$emit('update:visible', false);
    expect(page.emitted('close')).toBeUndefined();
    form.vm.$emit('busy', false); form.vm.$emit('changed'); await flushPromises();
    expect(page.emitted('changed')).toHaveLength(1);
    expect(list).toHaveBeenCalledTimes(2);
    await page.get('.repository-add-options button:first-child').trigger('click');
    expect(page.findComponent(RepositoryCloneForm).exists()).toBe(false);
    expect(page.find('#repository-path').exists()).toBe(true);
  });

});
