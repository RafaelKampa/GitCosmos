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
  expect(groups[0]!.findAll('.file-change')).toHaveLength(3);
  expect(groups[1]!.findAll('.file-change')).toHaveLength(2);
  expect(wrapper.text()).toContain('← original');
  await groups[1]!.get('.file-change').trigger('click');
  expect(wrapper.emitted('select')![0]).toEqual([{ path: file.path, area: 'staged' }]);
  await wrapper.setProps({ selected: { path: file.path, area: 'staged' } });
  expect(groups[1]!.get('.file-change').attributes('aria-pressed')).toBe('true');
  expect(groups[0]!.get('.file-change').attributes('aria-pressed')).toBe('false');
  await groups[0]!.get('.file-change').trigger('click');
  expect(wrapper.emitted('select')![1]).toEqual([{ path: file.path, area: 'working' }]);
  await wrapper.setProps({ selected: { path: 'other', area: 'working' } });
  expect(wrapper.findAll('button[aria-pressed="true"]')).toHaveLength(0);
});

it('explains empty groups', () => {
  wrapper = mount(FileChangesList, { props: { selected: null, files: [] } });
  expect(wrapper.findAll('.empty-files')).toHaveLength(2);
  expect(wrapper.find('.file-change').exists()).toBe(false);
  expect(wrapper.findAll('.move-files').every(button => button.attributes('disabled') !== undefined)).toBe(true);
});

it('moves the selected file or the eligible group and allows collapsing folders', async () => {
  wrapper = mount(FileChangesList, { props: { selected: null, files: [file, { ...file, path: 'folder/new.txt', indexStatus: '.', workTreeStatus: '?' }] } });
  const groups = wrapper.findAll('section');
  await groups[0]!.get('.move-files').trigger('click');
  expect(wrapper.emitted('move')![0]).toEqual([{ operation: 'stage', paths: ['folder/file.txt', 'folder/new.txt'] }]);
  await wrapper.setProps({ selected: { path: file.path, area: 'working' } });
  await groups[0]!.get('.move-files').trigger('click');
  expect(wrapper.emitted('move')![1]).toEqual([{ operation: 'stage', paths: [file.path] }]);
  await groups[1]!.get('.move-files').trigger('click');
  expect(wrapper.emitted('move')![2]).toEqual([{ operation: 'unstage', paths: [file.path] }]);
  await groups[0]!.get('.folder-toggle').trigger('click'); expect(groups[0]!.find('.file-change').exists()).toBe(false);
  await groups[0]!.get('.folder-toggle').trigger('click'); expect(groups[0]!.findAll('.file-change')).toHaveLength(2);
  await wrapper.setProps({ disabled: true }); expect(groups[0]!.get('.move-files').attributes('disabled')).toBeDefined();
});

it('distinguishes added, removed and modified files with semantic badges', () => {
  wrapper = mount(FileChangesList, { props: { selected: null, files: [
    { ...file, path: 'added', indexStatus: 'A', workTreeStatus: '.' },
    { ...file, path: 'removed', indexStatus: '.', workTreeStatus: 'D' }, file,
  ] } });
  expect(wrapper.get('.file-status.added').text()).toBe('+');
  expect(wrapper.get('.file-status.deleted').text()).toBe('−');
  expect(wrapper.get('.file-status.modified').text()).toBe('M');
});
