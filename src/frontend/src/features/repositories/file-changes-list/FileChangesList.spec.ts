import { afterEach, beforeEach, expect, it } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import FileChangesList from './FileChangesList.vue';
import type { GitFileChange } from '../../../core/repositories/models/repository-changes.model';

let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => wrapper?.unmount());
const file: GitFileChange = { path: 'folder/file.txt', originalPath: null, indexStatus: 'M', workTreeStatus: 'M', isUntracked: false, isConflicted: false, isSubmodule: false };

it('separates both sides of a changed file, renames, untracked files and conflicts', async () => {
  wrapper = mount(FileChangesList, { props: { selected: null, files: [file,
    { ...file, path: 'renamed', originalPath: 'original', indexStatus: 'R', workTreeStatus: '.' },
    { ...file, path: 'new', indexStatus: '.', workTreeStatus: '?', isUntracked: true },
    { ...file, path: 'conflict', indexStatus: 'U', workTreeStatus: 'U', isConflicted: true },
  ] } });
  const groups = wrapper.findAll('section');
  expect(groups[0]!.findAll('button')).toHaveLength(2);
  expect(groups[1]!.findAll('button')).toHaveLength(3);
  expect(wrapper.text()).toContain('← original');
  await groups[0]!.get('button').trigger('click');
  expect(wrapper.emitted('select')![0]).toEqual([{ path: file.path, area: 'staged' }]);
  await wrapper.setProps({ selected: { path: file.path, area: 'staged' } });
  expect(groups[0]!.get('button').attributes('aria-pressed')).toBe('true');
  expect(groups[1]!.get('button').attributes('aria-pressed')).toBe('false');
  await groups[1]!.get('button').trigger('click');
  expect(wrapper.emitted('select')![1]).toEqual([{ path: file.path, area: 'working' }]);
  await wrapper.setProps({ selected: { path: 'other', area: 'working' } });
  expect(wrapper.findAll('button[aria-pressed="true"]')).toHaveLength(0);
});

it('explains empty groups', () => {
  wrapper = mount(FileChangesList, { props: { selected: null, files: [] } });
  expect(wrapper.findAll('.empty-files')).toHaveLength(2);
  expect(wrapper.find('button').exists()).toBe(false);
});
