import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import Dialog from 'primevue/dialog';
import CatalogAddDialog from './CatalogAddDialog.vue';
import FolderPathField from '../../../shared/components/folder-path-field/FolderPathField.vue';
import RepositoryCloneForm from '../../workspaces/repository-clone-form/RepositoryCloneForm.vue';
import { CatalogApiService } from '../../../core/catalog/catalog-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { RepositoryImportResponse } from '../../../core/workspaces/models/responses/repository-import-response.model';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); }); afterEach(() => wrapper?.unmount());
const response: CustomResponse<RepositoryImportResponse> = { success: true, result: { items: [{localPath:'C:/Repo',status:'added',message:'Added'}] }, errors: [], messages: ['Registered'] };
function render() { wrapper = mount(CatalogAddDialog, { global: { plugins:[PrimeVue], stubs: { Dialog: { props:['visible','closable'], emits:['update:visible'], template:'<div><slot /></div>' }, Button: { props:['label','disabled'], template:'<button :disabled="disabled">{{ label }}</button>' }, RepositoryCloneForm: true } } }); return wrapper; }
it('registers a folder with outcome details and keeps the dialog open', async () => {
  const api = vi.spyOn(CatalogApiService.prototype,'addRepository').mockResolvedValue(response); const page=render();
  await page.get('input').setValue(' C:/Repo '); await page.get('form').trigger('submit'); await flushPromises();
  expect(api).toHaveBeenCalledWith('C:/Repo',expect.any(AbortSignal)); expect(page.text()).toContain('Registered'); expect(page.text()).toContain('Added'); expect(page.emitted('changed')).toHaveLength(1);
  await page.get('.workspace-dialog-actions button').trigger('click'); expect(page.emitted('close')).toHaveLength(1);
});
it.each([new ApiError('Localized'),new Error('private'),null])('keeps input and reports failures: %s',async cause=>{
  const api=vi.spyOn(CatalogApiService.prototype,'addRepository'); if(cause)api.mockRejectedValue(cause);else api.mockResolvedValue({...response,result:null});
  const page=render(); await page.get('input').setValue('C:/Repo');await page.get('form').trigger('submit');await flushPromises();
  expect(page.get('[role="alert"]').text()).toBe(cause instanceof ApiError?cause.message:'Não foi possível concluir a operação com os repositórios.'); expect(page.get('input').element).toHaveProperty('value','C:/Repo');
});
it.each([true,false])('blocks duplicate requests and close and ignores late responses: %s',async success=>{
  let resolve!:(value:typeof response)=>void;let reject!:(reason:Error)=>void; const pending=new Promise<typeof response>((ok,fail)=>{resolve=ok;reject=fail;});
  const api=vi.spyOn(CatalogApiService.prototype,'addRepository').mockReturnValue(pending);const page=render();
  page.getComponent(FolderPathField).vm.$emit('busy',true);await flushPromises();await page.get('form').trigger('submit');expect(api).not.toHaveBeenCalled();
  page.getComponent(Dialog).vm.$emit('update:visible',false);expect(page.emitted('close')).toBeUndefined();
  page.getComponent(FolderPathField).vm.$emit('error','Picker');page.getComponent(FolderPathField).vm.$emit('busy',false);await flushPromises();
  await page.get('form').trigger('submit');await page.get('form').trigger('submit');expect(api).toHaveBeenCalledTimes(1);
  page.getComponent(Dialog).vm.$emit('update:visible',false);expect(page.emitted('close')).toBeUndefined();page.unmount();wrapper=undefined;expect(api.mock.calls[0]![1]!.aborted).toBe(true);
  if(success)resolve(response);else reject(new Error('cancelled'));await flushPromises();expect(page.emitted('changed')).toBeUndefined();
});
it('uses clone mode without a workspace and reflects its busy state',async()=>{
  const page=render();await page.get('.add-mode button:last-child').trigger('click');const clone=page.getComponent(RepositoryCloneForm);expect(clone.props('workspaceId')).toBe(0);
  clone.vm.$emit('busy',true);await flushPromises();expect(page.getComponent(Dialog).props('closable')).toBe(false);
  clone.vm.$emit('changed');expect(page.emitted('changed')).toHaveLength(1);clone.vm.$emit('busy',false);await flushPromises();await page.get('.add-mode button:first-child').trigger('click');expect(page.find('form').exists()).toBe(true);
});

it('sends standalone registration with the current language through HTTP', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify(response)));
  vi.stubGlobal('fetch', fetchMock);
  try {
    useLanguageStore().setLanguage('es');
    const page = render();
    await page.get('input').setValue(' C:/Repo ');
    await page.get('form').trigger('submit'); await flushPromises();
    expect(fetchMock).toHaveBeenLastCalledWith('/api/RepositoryCatalog/AddRepository', expect.objectContaining({
      headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' },
      body: JSON.stringify({ localPath: 'C:/Repo' })
    }));
    expect(page.emitted('changed')).toHaveLength(1);
    expect(page.text()).toContain('Registered');
    useLanguageStore().setLanguage('en');
    await page.get('form').trigger('submit'); await flushPromises();
    expect(fetchMock).toHaveBeenLastCalledWith('/api/RepositoryCatalog/AddRepository', expect.objectContaining({
      headers: { 'Accept-Language': 'en', 'Content-Type': 'application/json' }
    }));
    expect(page.emitted('changed')).toHaveLength(2);
  } finally { vi.unstubAllGlobals(); }
});
