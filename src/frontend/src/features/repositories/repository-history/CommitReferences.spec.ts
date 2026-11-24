import { beforeEach, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import CommitReferences from './CommitReferences.vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });

it('renders typed pills and current-branch markers with the complete reference in their tooltip', async () => {
  const view = mount(CommitReferences, { props: { value: 'HEAD -> refs/heads/main, refs/remotes/origin/main, tag: refs/tags/v1, HEAD' } });
  expect(view.findAll('.reference-pill')).toHaveLength(4);
  expect(view.get('.branch.current').text()).toBe('main');
  expect(view.get('.branch.current .current-mark').attributes('data-icon')).toBe('check');
  expect(view.get('.remote .material-icon').attributes('data-icon')).toBe('cloud');
  expect(view.get('.tag .material-icon').attributes('data-icon')).toBe('label');
  expect(view.get('.remote').text()).toBe('origin/main');
  expect(view.get('.tag').text()).toBe('v1');
  expect(view.get('.head').text()).toBe('HEAD');
  expect(view.get('.remote').attributes('title')).toBe('Branch remota: refs/remotes/origin/main');
  useLanguageStore().setLanguage('en'); await view.vm.$nextTick();
  expect(view.get('.remote').attributes('title')).toBe('Remote branch: refs/remotes/origin/main');
  await view.setProps({ value: '' }); expect(view.find('.reference-pill').exists()).toBe(false);
  view.unmount();
});
