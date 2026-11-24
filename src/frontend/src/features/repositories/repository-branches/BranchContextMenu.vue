<script setup lang="ts">
import MaterialIcon from '../../../shared/components/material-icon/MaterialIcon.vue';
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue';
import type { BranchItem, BranchOperation } from '../../../core/repositories/models/repository-branch.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { branchActionLabels } from './branch-action-labels';
const props = defineProps<{ branch: BranchItem; currentBranch: string; disabled: boolean }>();
const emit = defineEmits<{ select: [operation: BranchOperation | 'copy']; open: [] }>();
const i18n = useLanguageStore(); const opened = ref(false); const trigger = ref<HTMLButtonElement>(); const menu = ref<HTMLElement>();
const position = ref({ left: '0px', top: '0px' });
const entries = computed(() => props.branch.isRemote
  ? ['checkoutRemote', 'worktree', 'pull', 'merge', 'rebase', 'interactiveRebase', 'pullRequest', 'create', 'tag', 'deleteRemote', 'copy'] as const
  : ['checkout', 'worktree', 'fastForward', 'push', 'pullRequest', 'merge', 'rebase', 'interactiveRebase', 'create', 'tag', 'setUpstream', 'unsetUpstream', 'rename', 'delete', 'copy'] as const);
function unavailable(operation: string) {
  if (props.disabled) return true;
  if (operation === 'checkout' || operation === 'delete') return props.branch.isCurrent;
  if (operation === 'worktree') return props.branch.isCheckedOut;
  if (operation === 'fastForward' || operation === 'unsetUpstream') return !props.branch.upstream;
  if (['merge', 'rebase', 'interactiveRebase', 'pull'].includes(operation)) return props.branch.isCurrent || !props.currentBranch;
  return false;
}
function label(operation: BranchOperation | 'copy') {
  const base = i18n.t(branchActionLabels[operation]);
  if (operation === 'fastForward' && props.branch.upstream) return base + ' · ' + props.branch.upstream.replace(/^refs\/remotes\//, '');
  return ['merge', 'rebase', 'interactiveRebase', 'pull'].includes(operation) ? base + ' · ' + props.currentBranch : base;
}
async function toggle() {
  if (opened.value) { close(); return; }
  const rect = trigger.value!.getBoundingClientRect();
  position.value = { left: Math.max(8, Math.min(rect.right, window.innerWidth - 312)) + 'px', top: Math.max(8, Math.min(rect.top, window.innerHeight - 470)) + 'px' };
  opened.value = true; emit('open'); await nextTick(); menu.value?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
}
function close() { opened.value = false; trigger.value?.focus(); }
function select(operation: BranchOperation | 'copy') { close(); emit('select', operation); }
function outside(event: Event) { if (opened.value && !menu.value?.contains(event.target as Node) && !trigger.value?.contains(event.target as Node)) opened.value = false; }
function keydown(event: KeyboardEvent) {
  if (event.key === 'Escape') { event.preventDefault(); close(); return; }
  if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
  event.preventDefault(); const buttons = Array.from(menu.value!.querySelectorAll<HTMLButtonElement>('button:not(:disabled)'));
  const current = buttons.indexOf(document.activeElement as HTMLButtonElement);
  const index = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : (current + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length;
  buttons[index]?.focus();
}
onMounted(() => document.addEventListener('pointerdown', outside));
onUnmounted(() => document.removeEventListener('pointerdown', outside));
</script>
<template>
  <button ref="trigger" class="branch-options" type="button" :disabled="props.disabled" :aria-label="i18n.t('branchOptions') + ': ' + branch.name" aria-haspopup="menu" :aria-expanded="opened" @click.stop="toggle"><MaterialIcon name="more_horiz" /></button>
  <Teleport to="body"><div v-if="opened" ref="menu" class="branch-context-menu" role="menu" :aria-label="i18n.t('branchOptions') + ': ' + branch.name" :style="position" @keydown="keydown">
    <button v-for="operation in entries" :key="operation" type="button" role="menuitem" :data-operation="operation" :disabled="unavailable(operation)" @click="select(operation)">{{ label(operation) }}</button>
  </div></Teleport>
</template>
<style scoped>
.branch-options { flex: 0 0 1.7rem; background: transparent; border: 1px solid transparent; color: var(--gc-text-muted); border-radius: .3rem; font-size: 1.2rem; cursor: pointer; }
.branch-options:hover { border-color: var(--gc-border); background: var(--gc-surface-raised); }
.branch-context-menu { position: fixed; z-index: 1200; width: min(19rem, calc(100vw - 16px)); max-height: min(29rem, calc(100vh - 16px)); overflow-y: auto; padding: .3rem; background: var(--gc-surface-raised); border: 1px solid var(--gc-border); border-radius: .4rem; }
.branch-context-menu button { display: block; width: 100%; padding: .5rem .65rem; border: 0; border-radius: .2rem; background: transparent; color: var(--gc-text); text-align: left; font-size: .8rem; }
.branch-context-menu button:hover:not(:disabled), .branch-context-menu button:focus-visible { background: var(--gc-selection-bg); }
</style>
