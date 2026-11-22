import { beforeEach, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import WorkspaceCreateForm from './WorkspaceCreateForm.vue';
import { useLanguageStore } from '../../../core/i18n/language.store';

beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });

it('labels and constrains the input and emits the trimmed name', async () => {
  const wrapper = mount(WorkspaceCreateForm, { props: { disabled: false }, global: { plugins: [PrimeVue] } });
  expect(wrapper.get('label').attributes('for')).toBe(wrapper.get('input').attributes('id'));
  expect(wrapper.get('input').attributes('maxlength')).toBe('100');
  expect(wrapper.get('input').attributes('required')).toBeDefined();
  await wrapper.get('input').setValue('  Team  ');
  await wrapper.get('form').trigger('submit');
  expect(wrapper.emitted('create')).toEqual([['Team']]);
  await wrapper.setProps({ disabled: true });
  expect(wrapper.get('input').attributes('disabled')).toBeDefined();
  expect(wrapper.get('button').attributes('disabled')).toBeDefined();
  useLanguageStore().setLanguage('en');
  await wrapper.vm.$nextTick();
  expect(wrapper.get('label').text()).toBe('Workspace name');
  wrapper.unmount();
});
