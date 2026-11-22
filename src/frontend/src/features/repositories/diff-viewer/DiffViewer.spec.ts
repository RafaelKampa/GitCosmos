import { afterEach, beforeEach, expect, it } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import DiffViewer from './DiffViewer.vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { FileDiff } from '../../../core/repositories/models/repository-changes.model';

let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => wrapper?.unmount());

it('renders additions and deletions as escaped text with line numbers', async () => {
  wrapper = mount(DiffViewer, { props: { diff: { path: 'file', area: 'working', kind: 'text', patch: '@@ -1 +1 @@\n-old\n+<img src=x onerror=alert(1)>\n' } } });
  expect(wrapper.findAll('.diff-line')).toHaveLength(3);
  expect(wrapper.get('.addition code').text()).toContain('<img src=x');
  expect(wrapper.find('img').exists()).toBe(false);
  expect(wrapper.get('.deletion .line-number').text()).toBe('1');
  expect(wrapper.get('.diff-viewer').attributes('tabindex')).toBe('0');
  await wrapper.setProps({ diff: { path: 'other', area: 'staged', kind: 'text', patch: '+updated' } });
  expect(wrapper.text()).toContain('+updated');
});

it.each(['binary', 'tooLarge', 'submodule', 'conflict', 'empty'] as const)('shows a translated explanation for %s', async kind => {
  const diff: FileDiff = { path: 'file', area: 'working', kind, patch: '' };
  wrapper = mount(DiffViewer, { props: { diff } });
  const portuguese = wrapper.get('[role="status"]').text();
  expect(portuguese.length).toBeGreaterThan(10); expect(wrapper.find('.diff-line').exists()).toBe(false);
  useLanguageStore().setLanguage('es'); await wrapper.vm.$nextTick();
  expect(wrapper.get('[role="status"]').text()).not.toBe(portuguese);
});
