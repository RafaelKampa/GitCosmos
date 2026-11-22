import { beforeEach, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import PrimeVue from 'primevue/config';
import Dialog from 'primevue/dialog';
import WorkspaceActionDialog from './WorkspaceActionDialog.vue';
import { useLanguageStore } from '../../../core/i18n/language.store';

const workspace = { id: 4, name: 'Team', createdAt: '2026-11-20T00:00:00Z', repositoryCount: 2 };
const dialogStub = {
  // Preserve PrimeVue's Boolean prop casting, including the bare "modal" attribute.
  props: {
    visible: Boolean,
    header: String,
    closable: { type: Boolean, default: true },
    closeOnEscape: { type: Boolean, default: true },
    modal: { type: Boolean, default: null },
    dismissableMask: { type: Boolean, default: false },
  },
  emits: ['update:visible'],
  template: '<div v-if="visible"><h2>{{ header }}</h2><button type="button" data-close-dialog :disabled="!closable" @click="$emit(\'update:visible\', false)">Close</button><slot /></div>' };
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });

it('prepopulates the name, constrains it and emits a trimmed rename', async () => {
  const wrapper = mount(WorkspaceActionDialog, { props: { workspace, mode: 'rename', busy: false, error: '' },
    global: { plugins: [PrimeVue], stubs: { Dialog: dialogStub } } });
  expect(wrapper.get('h2').text()).toBe('Renomear workspace');
  expect(wrapper.getComponent(Dialog).props()).toMatchObject({
    modal: true, closable: true, closeOnEscape: true, dismissableMask: false,
  });
  expect(wrapper.get<HTMLInputElement>('input').element.value).toBe('Team');
  expect(wrapper.get('label').attributes('for')).toBe('rename-workspace-name');
  expect(wrapper.get('input').attributes('maxlength')).toBe('100');
  expect(wrapper.get('input').attributes('required')).toBeDefined();
  await wrapper.get('input').setValue('  New Team  ');
  await wrapper.get('form').trigger('submit');
  expect(wrapper.emitted('rename')).toEqual([['New Team']]);
  expect(wrapper.emitted('delete')).toBeUndefined();
  await wrapper.setProps({ error: 'Duplicate name', busy: true });
  expect(wrapper.get('[role="alert"]').text()).toBe('Duplicate name');
  expect(wrapper.get<HTMLInputElement>('input').element.value).toBe('  New Team  ');
  expect(wrapper.get('input').attributes('disabled')).toBeDefined();
  expect(wrapper.get('form').attributes('aria-busy')).toBe('true');
  expect(wrapper.getComponent(Dialog).props()).toMatchObject({ closable: false, closeOnEscape: false, modal: true, dismissableMask: false });
  useLanguageStore().setLanguage('en'); await wrapper.vm.$nextTick();
  expect(wrapper.get('h2').text()).toBe('Rename workspace');
  wrapper.unmount();
});

it('requires a separate delete confirmation and explains that repository files are preserved', async () => {
  const wrapper = mount(WorkspaceActionDialog, { props: { workspace, mode: 'delete', busy: false, error: '' },
    global: { plugins: [PrimeVue], stubs: { Dialog: dialogStub } } });
  expect(wrapper.text()).toContain('Team');
  expect(wrapper.text()).toContain('arquivos no computador serão preservados');
  expect(wrapper.find('input').exists()).toBe(false);
  expect(wrapper.emitted('delete')).toBeUndefined();
  await wrapper.get('form').trigger('submit');
  expect(wrapper.emitted('delete')).toEqual([[]]);
  expect(wrapper.emitted('rename')).toBeUndefined();
  const cancel = wrapper.findAll('button').find(button => button.text() === 'Cancelar')!;
  expect(cancel.attributes('type')).toBe('button');
  await cancel.trigger('click');
  expect(wrapper.emitted('cancel')).toHaveLength(1);
  await wrapper.get('[data-close-dialog]').trigger('click');
  expect(wrapper.emitted('cancel')).toHaveLength(2);
  await wrapper.setProps({ busy: true });
  expect(wrapper.get('.workspace-danger-action').attributes('disabled')).toBeDefined();
  expect(wrapper.get('[data-close-dialog]').attributes('disabled')).toBeDefined();
  wrapper.unmount();
});
