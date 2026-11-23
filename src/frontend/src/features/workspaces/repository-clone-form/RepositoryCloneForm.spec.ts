import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import Button from 'primevue/button';
import FolderPathField from '../../../shared/components/folder-path-field/FolderPathField.vue';
import RepositoryCloneForm from './RepositoryCloneForm.vue';
import { RepositoryCloneApiService } from '../../../core/workspaces/repository-clone-api.service';
import { CatalogApiService } from '../../../core/catalog/catalog-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { RepositoryClonePlan } from '../../../core/workspaces/models/requests/repository-clone.model';
import type { CustomResponse } from '../../../shared/models/custom-response.model';

const plan: RepositoryClonePlan = { remoteUrl: 'https://github.com/RafaelKampa/GitCosmosTest2.git', parentPath: 'C:\\Repos', folderName: 'GitCosmosTest2', destinationPath: 'C:\\Repos\\GitCosmosTest2' };
const preview: CustomResponse<RepositoryClonePlan> = { success: true, result: plan, errors: [], messages: [] };
const completed = { success: true, result: { id: 5, name: 'GitCosmosTest2', localPath: plan.destinationPath }, messages: ['Cloned'], errors: [] };
let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { wrapper?.unmount(); wrapper = undefined; vi.unstubAllGlobals(); });
function render(disabled = false, workspaceId = 1) {
  wrapper = mount(RepositoryCloneForm, { props: { workspaceId, disabled }, global: { plugins: [PrimeVue], stubs: {
    Button: { props: ['label', 'disabled'], template: '<button :disabled="disabled">{{ label }}</button>' },
  } } });
  return wrapper;
}
async function fill(page: VueWrapper) { await page.get('#clone-repository-url').setValue(plan.remoteUrl); await page.get('#clone-parent-path').setValue(plan.parentPath); }

it('clones into the standalone catalog without creating an implicit workspace', async () => {
  vi.spyOn(CatalogApiService.prototype, 'preview').mockResolvedValue(preview);
  const clone = vi.spyOn(CatalogApiService.prototype, 'clone').mockResolvedValue(completed);
  const page = render(false, 0); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  expect(clone).not.toHaveBeenCalled(); await page.get('.clone-confirm').trigger('click'); await flushPromises();
  expect(clone).toHaveBeenCalledWith(expect.objectContaining({ workspaceId: 0, destinationPath: plan.destinationPath }), expect.any(AbortSignal));
  expect(page.emitted('changed')).toHaveLength(1);
});

it('previews the exact folder and clones only after explicit confirmation', async () => {
  const api = vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockResolvedValue(preview);
  const clone = vi.spyOn(RepositoryCloneApiService.prototype, 'clone').mockResolvedValue(completed);
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  expect(api).toHaveBeenCalledWith({ workspaceId: 1, remoteUrl: plan.remoteUrl, parentPath: plan.parentPath }, expect.any(AbortSignal));
  expect(page.get('.clone-preview code').text()).toBe('C:\\Repos\\GitCosmosTest2');
  expect(page.text()).toContain('Será criada uma nova pasta');
  expect(clone).not.toHaveBeenCalled();
  await page.get('.clone-confirm').trigger('click'); await flushPromises();
  expect(clone).toHaveBeenCalledWith({ workspaceId: 1, remoteUrl: plan.remoteUrl, parentPath: plan.parentPath, destinationPath: plan.destinationPath }, expect.any(AbortSignal));
  expect(page.emitted('changed')).toHaveLength(1);
  expect(page.get('[role="status"]').text()).toBe('Cloned');
});

it('uses the current language for standalone preview and clone HTTP requests', async () => {
  const fetchMock = vi.fn()
    .mockImplementationOnce(async () => new Response(JSON.stringify(preview)))
    .mockImplementationOnce(async () => new Response(JSON.stringify(completed)));
  vi.stubGlobal('fetch', fetchMock);
  useLanguageStore().setLanguage('es');
  const page = render(false, 0); await fill(page);
  await page.get('form').trigger('submit'); await flushPromises();
  expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/RepositoryCatalog/Preview', expect.objectContaining({
    headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' },
    body: JSON.stringify({ workspaceId: 0, remoteUrl: plan.remoteUrl, parentPath: plan.parentPath })
  }));
  expect(page.get('.clone-preview code').text()).toBe(plan.destinationPath);
  useLanguageStore().setLanguage('en');
  await page.get('.clone-confirm').trigger('click'); await flushPromises();
  expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/RepositoryCatalog/CloneRepository', expect.objectContaining({
    headers: { 'Accept-Language': 'en', 'Content-Type': 'application/json' },
    body: JSON.stringify({ workspaceId: 0, remoteUrl: plan.remoteUrl, parentPath: plan.parentPath, destinationPath: plan.destinationPath })
  }));
  expect(page.emitted('changed')).toHaveLength(1);
  expect(page.get('[role="status"]').text()).toBe('Cloned');
});

it('uses the selected language through real HTTP and invalidates the preview after editing', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify(preview)));
  vi.stubGlobal('fetch', fetchMock);
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  expect(fetchMock).toHaveBeenCalledWith('/api/RepositoryClone/Preview', expect.objectContaining({ headers: { 'Accept-Language': 'pt-BR', 'Content-Type': 'application/json' } }));
  await page.get('#clone-repository-url').setValue('https://example.com/other.git');
  expect(page.find('.clone-preview').exists()).toBe(false);
  page.getComponent(FolderPathField).vm.$emit('error', 'Picker error'); await flushPromises();
  expect(page.get('[role="alert"]').text()).toBe('Picker error');
});

it.each([new ApiError('Destination exists'), new Error('technical')])('preserves inputs when preview fails', async failure => {
  vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockRejectedValue(failure);
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  expect(page.get('[role="alert"]').text()).toBe(failure instanceof ApiError ? failure.message : 'Não foi possível concluir a clonagem. Verifique os dados e tente novamente.');
  expect(page.get<HTMLInputElement>('#clone-parent-path').element.value).toBe(plan.parentPath);
});
it('rejects incomplete previews and blocks requests while disabled or selecting a folder', async () => {
  const api = vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockResolvedValue({ ...preview, result: null });
  const page = render(true); await page.get('form').trigger('submit'); expect(api).not.toHaveBeenCalled();
  await page.setProps({ disabled: false });
  page.getComponent(FolderPathField).vm.$emit('busy', true); await flushPromises();
  await page.get('form').trigger('submit'); expect(api).not.toHaveBeenCalled();
  page.getComponent(FolderPathField).vm.$emit('busy', false); await flushPromises();
  await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  expect(page.get('[role="alert"]').text()).toContain('Não foi possível');
});
it('blocks duplicate confirmation and offers cancellation during cloning', async () => {
  vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockResolvedValue(preview);
  let reject!: (cause: Error) => void;
  const clone = vi.spyOn(RepositoryCloneApiService.prototype, 'clone').mockReturnValue(new Promise((_, fail) => { reject = fail; }));
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  await page.get('.clone-confirm').trigger('click'); await flushPromises();
  const confirm = page.findAllComponents(Button).find(button => button.classes().includes('clone-confirm'))!;
  confirm.vm.$emit('click'); await flushPromises();
  expect(clone).toHaveBeenCalledTimes(1);
  expect(page.text()).toContain('Clonando repositório');
  await page.get('.clone-cancel').trigger('click');
  expect(clone.mock.calls[0]![1]!.aborted).toBe(true);
  reject(new Error('aborted')); await flushPromises();
  expect(page.get('[role="status"]').text()).toContain('Cancelamento solicitado');
});
it('keeps the destination visible after a clone failure', async () => {
  vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockResolvedValue(preview);
  vi.spyOn(RepositoryCloneApiService.prototype, 'clone').mockRejectedValue(new ApiError('Clone failed'));
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  await page.get('.clone-confirm').trigger('click'); await flushPromises();
  expect(page.get('[role="alert"]').text()).toBe('Clone failed');
  expect(page.get('.clone-preview code').text()).toBe(plan.destinationPath);
});
it.each([true, false])('ignores late preview results after unmount (success=%s)', async success => {
  let resolve!: (value: typeof preview) => void; let reject!: (error: Error) => void;
  const api = vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockReturnValue(new Promise((ok, fail) => { resolve = ok; reject = fail; }));
  const page = render(); await fill(page); await page.get('form').trigger('submit'); page.unmount(); wrapper = undefined;
  expect(api.mock.calls[0]![1]!.aborted).toBe(true);
  if (success) resolve(preview); else reject(new Error('cancelled'));
  await flushPromises();
});
it.each([true, false])('cancels cloning when leaving and ignores its late completion (success=%s)', async success => {
  vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockResolvedValue(preview);
  let resolve!: (value: typeof completed) => void; let reject!: (error: Error) => void;
  const api = vi.spyOn(RepositoryCloneApiService.prototype, 'clone').mockReturnValue(new Promise((ok, fail) => { resolve = ok; reject = fail; }));
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  await page.get('.clone-confirm').trigger('click'); page.unmount(); wrapper = undefined;
  expect(api.mock.calls[0]![1]!.aborted).toBe(true);
  if (success) resolve(completed); else reject(new Error('cancelled'));
  await flushPromises(); expect(page.emitted('changed')).toBeUndefined();
});

it('rejects stale confirmation and confirmation while disabled, then handles unknown clone failures', async () => {
  vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockResolvedValue(preview);
  const clone = vi.spyOn(RepositoryCloneApiService.prototype, 'clone').mockRejectedValue(new Error('technical'));
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  const oldConfirm = page.findAllComponents(Button).find(button => button.classes().includes('clone-confirm'))!;
  await page.setProps({ disabled: true }); oldConfirm.vm.$emit('click'); await flushPromises();
  expect(clone).not.toHaveBeenCalled();
  await page.setProps({ disabled: false }); await page.get('#clone-repository-url').setValue('https://example.com/changed.git');
  oldConfirm.vm.$emit('click'); await flushPromises();
  expect(clone).not.toHaveBeenCalled();
  await page.get('form').trigger('submit'); await flushPromises(); await page.get('.clone-confirm').trigger('click'); await flushPromises();
  expect(page.get('[role="alert"]').text()).toBe('Não foi possível concluir a clonagem. Verifique os dados e tente novamente.');
});
it('ignores a stale preview after inputs change and prevents duplicate preview requests', async () => {
  let resolve!: (value: typeof preview) => void;
  const api = vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockReturnValue(new Promise(ok => { resolve = ok; }));
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await page.get('form').trigger('submit');
  expect(api).toHaveBeenCalledTimes(1);
  expect(page.text()).toContain('Verificando destino');
  page.getComponent(FolderPathField).vm.$emit('update:modelValue', 'C:\\other'); await flushPromises();
  expect(api.mock.calls[0]![1]!.aborted).toBe(true);
  resolve(preview); await flushPromises();
  expect(page.find('.clone-preview').exists()).toBe(false);
});
it('keeps a cancellation message when a successful response races with cancellation', async () => {
  vi.spyOn(RepositoryCloneApiService.prototype, 'preview').mockResolvedValue(preview);
  let resolve!: (value: typeof completed) => void;
  vi.spyOn(RepositoryCloneApiService.prototype, 'clone').mockReturnValue(new Promise(ok => { resolve = ok; }));
  const page = render(); await fill(page); await page.get('form').trigger('submit'); await flushPromises();
  await page.get('.clone-confirm').trigger('click'); await page.get('.clone-cancel').trigger('click');
  resolve(completed); await flushPromises();
  expect(page.get('[role="status"]').text()).toContain('Cancelamento solicitado');
  expect(page.emitted('changed')).toBeUndefined();
});

