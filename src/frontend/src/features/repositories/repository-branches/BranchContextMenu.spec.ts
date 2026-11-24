import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import BranchContextMenu from './BranchContextMenu.vue';
import type { BranchItem } from '../../../core/repositories/models/repository-branch.model';
const branch: BranchItem = { name: 'feature', sha: 'a'.repeat(40), upstream: 'refs/remotes/origin/feature', isCurrent: false, isCheckedOut: false, isRemote: false, remote: '', sourceBranch: 'feature' };
let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { view?.unmount(); view = undefined; });
function render(item = branch) { view = mount(BranchContextMenu, { props: { branch: item, currentBranch: 'main', disabled: false }, attachTo: document.body, global: { stubs: { Teleport: true } } }); return view; }
it('offers all local actions, supports keyboard navigation and closes on choice, Escape and outside click', async () => {
  const page = render(); await page.get('.branch-options').trigger('click');
  expect(page.findAll('[role="menuitem"]').map(item => item.attributes('data-operation'))).toEqual(['checkout', 'worktree', 'fastForward', 'push', 'pullRequest', 'merge', 'rebase', 'interactiveRebase', 'create', 'tag', 'setUpstream', 'unsetUpstream', 'rename', 'delete', 'copy']);
  expect(page.get('[data-operation="merge"]').text()).toContain('main');
  await page.get('[role="menu"]').trigger('pointerdown'); expect(page.find('[role="menu"]').exists()).toBe(true);
  await page.get('[role="menu"]').trigger('keydown', { key: 'End' }); expect(document.activeElement).toBe(page.get('[data-operation="copy"]').element);
  await page.get('[role="menu"]').trigger('keydown', { key: 'Home' }); expect(document.activeElement).toBe(page.get('[data-operation="checkout"]').element);
  await page.get('[role="menu"]').trigger('keydown', { key: 'ArrowDown' }); await page.get('[role="menu"]').trigger('keydown', { key: 'ArrowUp' });
  await page.get('[role="menu"]').trigger('keydown', { key: 'Tab' });
  await page.get('[data-operation="rename"]').trigger('click'); expect(page.emitted('select')![0]).toEqual(['rename']); expect(page.find('[role="menu"]').exists()).toBe(false);
  document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }));
  await page.get('.branch-options').trigger('click'); await page.get('[role="menu"]').trigger('keydown', { key: 'Escape' }); expect(document.activeElement).toBe(page.get('.branch-options').element);
  await page.get('.branch-options').trigger('click'); await page.get('.branch-options').trigger('pointerdown'); await page.get('.branch-options').trigger('click'); expect(page.find('[role="menu"]').exists()).toBe(false);
  await page.get('.branch-options').trigger('click'); document.body.dispatchEvent(new Event('pointerdown', { bubbles: true })); await page.vm.$nextTick(); expect(page.find('[role="menu"]').exists()).toBe(false);
});
it('offers the different remote menu and disables invalid operations for the current branch and missing upstream', async () => {
  const page = render({ ...branch, isRemote: true, name: 'origin/feature', remote: 'origin' }); await page.get('.branch-options').trigger('click');
  expect(page.findAll('[role="menuitem"]').map(item => item.attributes('data-operation'))).toEqual(['checkoutRemote', 'worktree', 'pull', 'merge', 'rebase', 'interactiveRebase', 'pullRequest', 'create', 'tag', 'deleteRemote', 'copy']);
  await page.setProps({ branch: { ...branch, isCurrent: true, isCheckedOut: true, upstream: null }, currentBranch: '' });
  for (const operation of ['checkout', 'worktree', 'fastForward', 'merge', 'unsetUpstream', 'delete']) expect(page.get('[data-operation="' + operation + '"]').attributes('disabled')).toBeDefined();
  await page.setProps({ disabled: true }); expect(page.findAll('[role="menuitem"]').every(item => item.attributes('disabled') !== undefined)).toBe(true);
  await page.get('[role="menu"]').trigger('keydown', { key: 'ArrowDown' });
});
