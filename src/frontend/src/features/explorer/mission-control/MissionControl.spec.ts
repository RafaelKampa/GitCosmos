import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import MissionControl from './MissionControl.vue';
import CatalogAddDialog from '../catalog-add-dialog/CatalogAddDialog.vue';
import { CatalogApiService } from '../../../core/catalog/catalog-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { CatalogSnapshot } from '../../../core/catalog/catalog.model';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); }); afterEach(() => wrapper?.unmount());
const empty: CustomResponse<CatalogSnapshot> = { success: true, result: { workspaces: [], repositories: [] }, messages: [], errors: [] };
function render(all = false) { wrapper = mount(MissionControl, { props: { all }, global: { plugins: [PrimeVue], stubs: { RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' }, Button: { props: ['label','disabled'], template: '<button :disabled="disabled">{{ label }}</button>' }, CatalogAddDialog: true } } }); return wrapper; }
it.each([{workspaces:null,repositories:[]},{workspaces:[],repositories:null}])('rejects partial overview collections: %s',async result=>{
  vi.spyOn(CatalogApiService.prototype,'getOverview').mockResolvedValue({...empty,result:result as unknown as CatalogSnapshot});const page=render();await flushPromises();expect(page.get('[role="alert"]').text()).toContain('Não foi possível');
});
it('shares overview, offers actual workspace navigation and opens standalone registration', async () => {
  const api = vi.spyOn(CatalogApiService.prototype, 'getOverview').mockResolvedValue({ ...empty, result: { workspaces: [{id:1,name:'Team'}], repositories: [] } });
  const page = render(); await flushPromises(); expect(page.get('h1').text()).toBe('Mission Control'); expect(page.get('.workspace-tile').attributes('href')).toBe('/workspaces/1');
  await page.get('.page-actions button:last-child').trigger('click'); const dialog = page.getComponent(CatalogAddDialog);
  dialog.vm.$emit('changed'); await flushPromises(); expect(api).toHaveBeenCalledTimes(2); dialog.vm.$emit('close'); await flushPromises(); expect(page.findComponent(CatalogAddDialog).exists()).toBe(false);
  await page.get('.page-actions button:first-child').trigger('click'); await flushPromises(); expect(api).toHaveBeenCalledTimes(3);
  await page.setProps({ all: true }); expect(page.get('h1').text()).toBe('Todos os repositórios'); expect(page.find('.mission-workspaces').exists()).toBe(false);
});
it.each([new ApiError('Localized'), new Error('private'), null])('shows real errors rather than sample repositories: %s', async cause => {
  const api = vi.spyOn(CatalogApiService.prototype, 'getOverview'); if (cause) api.mockRejectedValueOnce(cause); else api.mockResolvedValueOnce({ ...empty, result: null }); api.mockResolvedValue(empty);
  const page = render(); await flushPromises(); expect(page.get('[role="alert"]').text()).toBe(cause instanceof ApiError ? cause.message : 'Não foi possível carregar o catálogo de repositórios.');
  await page.get('.page-actions button:first-child').trigger('click'); await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false); expect(page.text()).toContain('Nenhum workspace cadastrado');
});
it.each([true,false])('aborts stale overview responses on language changes and unmount: %s', async success => {
  let resolve!: (value: typeof empty) => void; let reject!: (error: Error) => void;
  const pending = new Promise<typeof empty>((ok,fail) => { resolve = ok; reject = fail; });
  const api = vi.spyOn(CatalogApiService.prototype, 'getOverview').mockReturnValueOnce(pending).mockResolvedValue(empty);
  const page = render(true); useLanguageStore().setLanguage('en'); await flushPromises(); expect(api.mock.calls[0]![0]!.aborted).toBe(true);
  if (success) resolve(empty); else reject(new Error('cancelled')); await flushPromises(); page.unmount(); wrapper = undefined; expect(api.mock.calls[1]![0]!.aborted).toBe(true);
});
