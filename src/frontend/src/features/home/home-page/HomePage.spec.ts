import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import HomePage from './HomePage.vue';
import { SystemApiService } from '../../../core/system/system-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
import type { SystemStatusResponse } from '../../../core/system/models/responses/system-status-response.model';

const response: CustomResponse<SystemStatusResponse> = {
  success: true, errors: [], messages: ['Backend ready'], result: { name: 'GitCosmos', version: 'dev', language: 'en' },
};

describe('home', () => {
  let wrapper: VueWrapper | undefined;
  beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
  afterEach(() => { wrapper?.unmount(); wrapper = undefined; });
  function render() {
    wrapper = mount(HomePage, { global: { stubs: { Button: { props: ['label', 'disabled'], template: '<button :disabled="disabled">{{ label }}</button>' } } } });
    return wrapper;
  }
  it('shows loading, then the localized backend message', async () => {
    let resolve!: (value: typeof response) => void;
    vi.spyOn(SystemApiService.prototype, 'getStatus').mockReturnValue(new Promise(r => { resolve = r; }));
    const page = render();
    await flushPromises();
    expect(page.text()).toContain('Verificando');
    expect(page.get('button').attributes('disabled')).toBeDefined();
    resolve(response); await flushPromises();
    expect(page.get('[role="status"]').text()).toBe('Backend ready');
    expect(page.get('button').attributes('disabled')).toBeUndefined();
  });
  it('preserves backend errors and lets the user retry', async () => {
    const request = vi.spyOn(SystemApiService.prototype, 'getStatus').mockRejectedValueOnce(new ApiError('Erro localizado')).mockResolvedValue(response);
    const page = render(); await flushPromises();
    expect(page.get('[role="alert"]').text()).toBe('Erro localizado');
    await page.get('button').trigger('click'); await flushPromises();
    expect(request).toHaveBeenCalledTimes(2); expect(page.get('[role="status"]').text()).toBe('Backend ready');
  });
  it('uses a translated fallback for unknown failures and reloads on language change', async () => {
    const request = vi.spyOn(SystemApiService.prototype, 'getStatus').mockRejectedValue(new Error('technical'));
    const page = render(); await flushPromises();
    expect(page.get('[role="alert"]').text()).toContain('Não foi possível');
    useLanguageStore().setLanguage('en'); await flushPromises();
    expect(request).toHaveBeenCalledTimes(2);
    expect(page.get('[role="alert"]').text()).toBe('Could not connect to the local application.');
  });
  it.each([true, false])('ignores results after unmount (success=%s)', async succeeds => {
    let resolve!: (value: typeof response) => void;
    let reject!: (value: Error) => void;
    const request = vi.spyOn(SystemApiService.prototype, 'getStatus').mockReturnValue(new Promise((ok, fail) => { resolve = ok; reject = fail; }));
    const page = render(); page.unmount(); wrapper = undefined;
    expect(request.mock.calls[0]![0]!.aborted).toBe(true);
    if (succeeds) resolve(response); else reject(new Error('cancelled'));
    await flushPromises();
  });
  it('cancels a previous request when changing language', async () => {
    const pending: ((value: typeof response) => void)[] = [];
    const request = vi.spyOn(SystemApiService.prototype, 'getStatus').mockImplementation(() => new Promise(r => pending.push(r)));
    const page = render();
    useLanguageStore().setLanguage('es'); await flushPromises();
    expect(request.mock.calls[0]![0]!.aborted).toBe(true);
    pending[0]!(response); pending[1]!(response); await flushPromises();
    expect(page.get('[role="status"]').text()).toBe('Backend ready');
  });
});
