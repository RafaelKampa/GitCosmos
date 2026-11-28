<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import type { FileDiff } from '../../../core/repositories/models/repository-changes.model';
import type { DiffTextContext } from '../../../core/repositories/models/local-files.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { selectableRows } from './line-selection';
const props = defineProps<{ diff: FileDiff; selectable?: boolean; selected?: readonly number[]; disabled?: boolean }>();
const emit = defineEmits<{ selection: [ids: number[]]; action: [operation: 'move' | 'discard', ids: number[]]; context: [value: DiffTextContext] }>();
const i18n = useLanguageStore(); const root = ref<HTMLElement>(); const hovered = ref<number | null>(null); const top = ref(0); const nativeSelection = ref(false);
const rows = computed(() => selectableRows(props.diff.patch).filter(row => row.type === 'addition' || row.type === 'deletion' || row.type === 'context'));
const groups = computed(() => [...new Set(rows.value.map(row => row.hunk))].map(id => ({ id, rows: rows.value.filter(row => row.hunk === id), ids: rows.value.filter(row => row.hunk === id && row.selectable).map(row => row.id) })));
const messages = { binary: 'diffBinary', tooLarge: 'diffTooLarge', submodule: 'diffSubmodule', conflict: 'diffConflict', empty: 'diffEmpty' } as const;
function selectedRange() {
  const selection = window.getSelection();
  if (!selection || selection.isCollapsed || !selection.rangeCount || !root.value || !root.value.contains(selection.anchorNode) || !root.value.contains(selection.focusNode)) return null;
  const range = selection.getRangeAt(0);
  const pieces = [...root.value.querySelectorAll<HTMLElement>('code[data-line-id]')].filter(code => range.intersectsNode(code)).map(code => {
    const part = document.createRange(); part.selectNodeContents(code);
    if (code.contains(range.startContainer)) part.setStart(range.startContainer, range.startOffset);
    if (code.contains(range.endContainer)) part.setEnd(range.endContainer, range.endOffset);
    return { id: Number(code.dataset.lineId), text: part.toString() };
  }).filter(part => part.text.length > 0);
  const pieceIds = new Set(pieces.map(piece => piece.id)); const selectedRows = rows.value.filter(row => pieceIds.has(row.id));
  if (!selectedRows.length) return null;
  const numbers = selectedRows.map(row => row.newLine ?? row.oldLine).filter((value): value is number => value !== null);
  return { ids: selectedRows.filter(row => row.selectable).map(row => row.id), start: Math.min(...numbers), end: Math.max(...numbers), text: pieces.map(piece => piece.text).join('\n') };
}
function selectionChanged() {
  if (!props.selectable || props.disabled) return;
  const range = selectedRange(); nativeSelection.value = range !== null; emit('selection', range?.ids ?? []);
}
function context(event: MouseEvent) {
  if (!props.selectable || props.disabled) return;
  const range = selectedRange(); if (!range) return;
  event.preventDefault(); nativeSelection.value = true; emit('selection', range.ids); emit('context', { ...range, x: event.clientX, y: event.clientY });
}
function hover(hunk: number, event: MouseEvent) { hovered.value = hunk; top.value = (event.currentTarget as HTMLElement).offsetTop; }
function action(operation: 'move' | 'discard', ids: number[]) {
  if (!props.disabled) emit('action', operation, props.selected?.length ? [...props.selected] : ids);
}
watch(() => props.diff, () => { hovered.value = null; top.value = 0; nativeSelection.value = false; });
onMounted(() => document.addEventListener('selectionchange', selectionChanged));
onUnmounted(() => document.removeEventListener('selectionchange', selectionChanged));
</script>
<template>
  <div v-if="diff.kind === 'text' && rows.length" ref="root" class="diff-viewer" tabindex="0" role="region" :aria-label="i18n.t('changesDiff')" @mouseup="selectionChanged" @keyup="selectionChanged" @contextmenu="context">
    <div v-for="group in groups" :key="group.id" class="diff-hunk" :tabindex="selectable ? 0 : undefined" @focus="hovered = group.id; top = 0" @mouseleave="hovered = null">
      <div v-if="selectable && hovered === group.id && (nativeSelection ? selected?.length : group.ids.length || selected?.length)" class="hunk-actions" :style="{ top: top + 'px' }" role="toolbar" :aria-label="i18n.t(selected?.length ? 'localSelectedLines' : 'localWholeBlock')" @mousedown.prevent>
        <button class="hunk-move" :disabled="disabled" @click="action('move', group.ids)">{{ i18n.t(diff.area === 'staged' ? 'editUnstage' : 'editStage') }}<span v-if="selected?.length"> ({{ selected.length }})</span></button>
        <button v-if="diff.area === 'working'" class="hunk-discard" :disabled="disabled" @click="action('discard', group.ids)">{{ i18n.t('localDiscard') }}</button>
      </div>
      <div v-for="line in group.rows" :key="line.id" class="diff-line" :class="[line.type, { 'line-selected': selected?.includes(line.id) }]" @mouseenter="hover(group.id, $event)">
        <span class="line-number" aria-hidden="true">{{ line.oldLine }}</span><span class="line-number" aria-hidden="true">{{ line.newLine }}</span><code :data-line-id="line.id">{{ line.text }}</code>
      </div>
    </div>
  </div>
  <p v-else class="diff-notice" role="status">{{ i18n.t(diff.kind === 'text' ? 'diffEmpty' : messages[diff.kind]) }}</p>
</template>
<style scoped>
.diff-viewer { overflow: auto; max-height: 70vh; background: var(--gc-bg); font-size: .85rem; line-height: 1.6; }
.diff-viewer:focus-visible, .diff-hunk:focus-visible { outline: 2px solid var(--gc-violet); outline-offset: -2px; }
.diff-hunk { position: relative; min-width: 100%; }
.diff-line { display: flex; min-width: max-content; white-space: pre; font-family: monospace; }
.diff-line code { padding-inline: .75rem; }
.diff-line code::selection { background: var(--gc-selection-bg); color: var(--gc-text); }
.line-number { width: 3.5rem; flex-shrink: 0; text-align: right; padding-inline: .5rem; color: var(--gc-text-muted); user-select: none; border-right: 1px solid var(--gc-divider); }
.addition { background: var(--gc-success-bg); color: var(--gc-success); }
.deletion { background: var(--gc-danger-bg); color: var(--gc-danger); }
.line-selected { box-shadow: inset 3px 0 var(--gc-violet); }
.hunk-actions { position: absolute; right: .5rem; z-index: 2; display: flex; gap: .25rem; background: var(--gc-surface-raised); border: 1px solid var(--gc-border); border-radius: .3rem; padding: .15rem; }
.hunk-actions button { background: var(--gc-surface); border: 1px solid var(--gc-border); color: var(--gc-text); border-radius: .2rem; padding: .25rem .5rem; font: inherit; }
.diff-notice { padding: 1rem; color: var(--gc-text-muted); }
</style>
