import { beforeEach, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import FileStatusBadge from './FileStatusBadge.vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });

it.each([
  ['A', '+', 'added'], ['D', '−', 'deleted'], ['M', 'M', 'modified'], ['R100', 'R', 'modified'],
  ['U', 'U', 'deleted'], ['?', '+', 'added'], ['C100', 'C100', 'modified'], ['', '?', 'modified'],
])('renders %s with its matching symbol, semantic color and accessible description', (status, symbol, tone) => {
  const view = mount(FileStatusBadge, { props: { status } });
  expect(view.text()).toBe(symbol); expect(view.classes()).toContain(tone);
  expect(view.attributes('aria-label')).toMatch(/.{3}/u);
  expect(view.attributes('title')).toContain('(' + status + ')'); view.unmount();
});

it('updates the badge and localizes its description when the status or language changes', async () => {
  const view = mount(FileStatusBadge, { props: { status: 'M' } });
  await view.setProps({ status: 'A' }); expect(view.text()).toBe('+'); expect(view.classes()).toContain('added');
  useLanguageStore().setLanguage('en'); await view.vm.$nextTick(); expect(view.attributes('aria-label')).toBe('Added');
  useLanguageStore().setLanguage('es'); await view.vm.$nextTick(); expect(view.attributes('aria-label')).toBe('Añadidos');
  view.unmount();
});
