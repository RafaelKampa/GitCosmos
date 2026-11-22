import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import FolderPathField from './FolderPathField.vue';
import { LocalFolderApiService } from '../../../core/file-system/local-folder-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { FolderSelectionResponse } from '../../../core/file-system/models/folder-selection.model';
import type { CustomResponse } from '../../models/custom-response.model';

let wrapper: VueWrapper | undefined;
const selected: CustomResponse<FolderSelectionResponse> = { success: true, result: { localPath: 'C:\\selected', cancelled: false }, messages: [], errors: [] };
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { wrapper?.unmount(); wrapper = undefined; vi.unstubAllGlobals(); });
function render(disabled = false) {
  wrapper = mount(FolderPathField, { props: { modelValue: '  C:\\old  ', id: 'path', label: 'Path', help: 'Help', disabled },
    global: { plugins: [PrimeVue], stubs: { Button: { props: ['label', 'disabled'], template: '<button :disabled="disabled">{{ label }}</button>' } } } });
  return wrapper;
}

it('uses the native API with the current language, no automatic timeout, and fills the full selected path', async () => {
  useLanguageStore().setLanguage('es');
  const timeout = vi.spyOn(AbortSignal, 'timeout');
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(selected)));
  vi.stubGlobal('fetch', fetchMock);
  const page = render();
  expect(page.get('button').attributes('type')).toBe('button');
  expect(page.get('label').attributes('for')).toBe('path');
  await page.get('button').trigger('click'); await flushPromises();
  expect(fetchMock).toHaveBeenCalledWith('/api/LocalFolder/SelectFolder', expect.objectContaining({
    method: 'POST', body: JSON.stringify({ initialPath: 'C:\\old' }),
    headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' },
  }));
  expect(timeout).not.toHaveBeenCalled();
  expect(page.emitted('update:modelValue')).toEqual([['C:\\selected']]);
  expect(page.emitted('busy')).toEqual([[true], [false]]);
  expect(page.emitted('error')).toEqual([['']]);
});

it('keeps manual editing available and does not open a disabled picker', async () => {
  const api = vi.spyOn(LocalFolderApiService.prototype, 'selectFolder');
  const page = render(true);
  page.getComponent(Button).vm.$emit('click'); await flushPromises();
  expect(api).not.toHaveBeenCalled();
  await page.setProps({ disabled: false });
  await page.get('input').setValue('manual');
  expect(page.emitted('update:modelValue')).toContainEqual(['manual']);
  page.getComponent(InputText).vm.$emit('update:modelValue', undefined); await flushPromises();
  expect(page.emitted('update:modelValue')).toContainEqual(['']);
});

it('ignores a cancelled native selection and prevents duplicate requests', async () => {
  let resolve!: (value: typeof selected) => void;
  const api = vi.spyOn(LocalFolderApiService.prototype, 'selectFolder').mockReturnValue(new Promise(ok => { resolve = ok; }));
  const page = render();
  await page.get('button').trigger('click');
  page.getComponent(Button).vm.$emit('click'); await flushPromises();
  expect(api).toHaveBeenCalledTimes(1);
  expect(page.get('input').attributes('disabled')).toBeDefined();
  expect(page.text()).toContain('Selecionando');
  resolve({ ...selected, result: { localPath: null, cancelled: true } }); await flushPromises();
  expect(page.emitted('update:modelValue')).toBeUndefined();
  expect(page.get<HTMLInputElement>('input').element.value).toBe('  C:\\old  ');
});

it.each([null, { localPath: null, cancelled: false }])('rejects an incomplete folder selection', async result => {
  vi.spyOn(LocalFolderApiService.prototype, 'selectFolder').mockResolvedValue({ ...selected, result });
  const page = render(); await page.get('button').trigger('click'); await flushPromises();
  expect(page.emitted('error')!.at(-1)).toEqual(['Não foi possível selecionar a pasta. Você pode digitar o caminho manualmente.']);
});
it.each([new ApiError('Native picker unavailable'), new Error('technical')])('reports errors without replacing the typed path', async failure => {
  vi.spyOn(LocalFolderApiService.prototype, 'selectFolder').mockRejectedValue(failure);
  const page = render(); await page.get('button').trigger('click'); await flushPromises();
  expect(page.emitted('error')!.at(-1)).toEqual([failure instanceof ApiError ? failure.message : 'Não foi possível selecionar a pasta. Você pode digitar o caminho manualmente.']);
  expect(page.emitted('update:modelValue')).toBeUndefined();
});
it.each([true, false])('cancels selection on unmount and ignores late completion (success=%s)', async success => {
  let resolve!: (value: typeof selected) => void; let reject!: (value: Error) => void;
  const api = vi.spyOn(LocalFolderApiService.prototype, 'selectFolder').mockReturnValue(new Promise((ok, fail) => { resolve = ok; reject = fail; }));
  const page = render(); await page.get('button').trigger('click');
  page.unmount(); wrapper = undefined;
  expect(api.mock.calls[0]![1]!.aborted).toBe(true);
  if (success) resolve(selected); else reject(new Error('cancelled'));
  await flushPromises();
  expect(page.emitted('update:modelValue')).toBeUndefined();
});
