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
  expect(wrapper.findAll('.diff-line')).toHaveLength(2);
  expect(wrapper.get('.addition code').text()).toContain('<img src=x');
  expect(wrapper.find('img').exists()).toBe(false);
  expect(wrapper.get('.deletion .line-number').text()).toBe('1');
  expect(wrapper.get('.diff-viewer').attributes('tabindex')).toBe('0');
  await wrapper.setProps({ diff: { path: 'other', area: 'staged', kind: 'text', patch: '+updated' } });
  expect(wrapper.text()).toContain('+updated');
});

it('hides patch metadata and hunk headers while preserving file code and original line numbers', () => {
  wrapper = mount(DiffViewer, { props: { diff: { path: 'file.cs', area: 'working', kind: 'text', patch: 'diff --git a/file.cs b/file.cs\nindex abc..def 100644\n--- a/file.cs\n+++ b/file.cs\n@@ -10,2 +20,2 @@ method\n context\n--- code removed\n+++ code added\n\\ No newline at end of file\n' } } });
  expect(wrapper.findAll('.diff-line')).toHaveLength(3);
  expect(wrapper.text()).not.toContain('diff --git'); expect(wrapper.text()).not.toContain('index abc');
  expect(wrapper.text()).not.toContain('a/file.cs'); expect(wrapper.text()).not.toContain('b/file.cs');
  expect(wrapper.text()).not.toContain('@@'); expect(wrapper.text()).not.toContain('No newline');
  expect(wrapper.get('.context code').text()).toBe('context');
  expect(wrapper.get('.deletion code').text()).toBe('--- code removed');
  expect(wrapper.get('.addition code').text()).toBe('+++ code added');
  expect(wrapper.get('.context .line-number').text()).toBe('10');
  expect(wrapper.get('.addition .line-number:last-of-type').text()).toBe('21');
});

it('explains commits that only change file metadata without showing technical headers', () => {
  wrapper = mount(DiffViewer, { props: { diff: { path: 'renamed', area: 'working', kind: 'text', patch: 'diff --git a/old b/new\nsimilarity index 100%\nrename from old\nrename to new\n' } } });
  expect(wrapper.find('.diff-line').exists()).toBe(false);
  expect(wrapper.get('[role="status"]').text().length).toBeGreaterThan(10);
  expect(wrapper.text()).not.toContain('rename from');
});

it.each(['binary', 'tooLarge', 'submodule', 'conflict', 'empty'] as const)('shows a translated explanation for %s', async kind => {
  const diff: FileDiff = { path: 'file', area: 'working', kind, patch: '' };
  wrapper = mount(DiffViewer, { props: { diff } });
  const portuguese = wrapper.get('[role="status"]').text();
  expect(portuguese.length).toBeGreaterThan(10); expect(wrapper.find('.diff-line').exists()).toBe(false);
  useLanguageStore().setLanguage('es'); await wrapper.vm.$nextTick();
  expect(wrapper.get('[role="status"]').text()).not.toBe(portuguese);
});
