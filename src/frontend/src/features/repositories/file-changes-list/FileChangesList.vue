<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { GitFileChange, DiffSelection, DiffArea } from '../../../core/repositories/models/repository-changes.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import ResizeHandle from '../../../shared/components/resize-handle/ResizeHandle.vue';
import MaterialIcon from '../../../shared/components/material-icon/MaterialIcon.vue';
import FileStatusBadge from '../../../shared/components/file-status-badge/FileStatusBadge.vue';
import { usePanelLayout } from '../../../core/preferences/panel-layout.store';
import { buildFileTree } from './file-tree';
import { visibleChangeNodes, selectTreeItems, selectedFilePaths, expandSelectedKeys, type VisibleChangeNode } from './tree-selection';
const props = withDefaults(defineProps<{ files: readonly GitFileChange[]; selected: DiffSelection | null; disabled?: boolean }>(), { disabled: false });
const emit = defineEmits<{ select: [selection: DiffSelection]; move: [value: { operation: 'stage' | 'unstage'; paths: string[] }]; context: [value: { path: string; area: DiffArea; paths: string[]; all: string[]; x: number; y: number }] }>();
const i18n = useLanguageStore(); const layout = usePanelLayout();
const collapsed = ref<Record<DiffArea, string[]>>({ working: [], staged: [] });
const active = ref<DiffArea | null>(null); const chosen = ref<string[]>([]); const anchor = ref<string | null>(null);
let emitted: DiffSelection | undefined;
const groups = computed(() => [
  { area: 'working' as const, label: 'editUnstaged' as const, operation: 'stage' as const, files: props.files.filter(file => file.workTreeStatus !== '.') },
  { area: 'staged' as const, label: 'editStaged' as const, operation: 'unstage' as const, files: props.files.filter(file => file.indexStatus !== '.' && !file.isConflicted) },
].map(group => ({ ...group, rows: visibleChangeNodes(buildFileTree(group.files), collapsed.value[group.area]) })));
function keys(area: DiffArea) { return active.value === area ? chosen.value : active.value === null && props.selected?.area === area ? ['f:' + props.selected.path] : []; }
function paths(area: DiffArea, files: readonly GitFileChange[], all = false) {
  const available = files.filter(file => !file.isConflicted).map(file => file.path);
  const selection = keys(area); return all ? available : selection.length ? selectedFilePaths(selection, available) : active.value === area ? [] : available;
}
function choose(row: VisibleChangeNode, area: DiffArea, event: MouseEvent) {
  if (props.disabled) return;
  const previous = active.value === area ? chosen.value : keys(area);
  const rows = groups.value.find(group => group.area === area)!.rows;
  const control = event.ctrlKey || event.metaKey;
  let next = selectTreeItems(rows.map(item => item.key), previous, active.value === area ? anchor.value : null, row.key, event.shiftKey, control);
  if (control && !event.shiftKey && previous.includes(row.key)) next = next.filter(key => !(key.startsWith('d:') && row.node.path.startsWith(key.slice(2) + '/')) && !(!row.node.file && key.slice(2).startsWith(row.node.path + '/')));
  chosen.value = expandSelectedKeys(next, buildFileTree(groups.value.find(group => group.area === area)!.files));
  active.value = area; if (!event.shiftKey) anchor.value = row.key;
  if (row.node.file && chosen.value.includes(row.key)) { emitted = { path: row.node.path, area }; emit('select', emitted); }
}
function toggle(path: string, area: DiffArea) {
  const current = collapsed.value[area]; collapsed.value[area] = current.includes(path) ? current.filter(value => value !== path) : [...current, path];
}
function context(row: VisibleChangeNode, area: DiffArea, event: MouseEvent) {
  if (props.disabled || !row.node.file) return;
  const group = groups.value.find(item => item.area === area)!;
  if (!selectedFilePaths(keys(area), group.files.map(file => file.path)).includes(row.node.path)) choose(row, area, new MouseEvent('click'));
  emit('context', { path: row.node.path, area, paths: paths(area, group.files), all: paths(area, group.files, true), x: event.clientX, y: event.clientY });
}
function keyboard(row: VisibleChangeNode, area: DiffArea, event: KeyboardEvent) {
  if (props.disabled) return;
  if (event.key === 'Enter') { event.preventDefault(); const group = groups.value.find(item => item.area === area)!; emit('move', { operation: group.operation, paths: paths(area, group.files) }); }
  else if (event.key === 'ContextMenu' || event.key === 'F10' && event.shiftKey) { event.preventDefault(); const rect = (event.target as HTMLElement).getBoundingClientRect(); context(row, area, new MouseEvent('contextmenu', { clientX: rect.left, clientY: rect.bottom })); }
}
watch(() => props.selected, value => {
  if (value?.path === emitted?.path && value?.area === emitted?.area) return;
  active.value = null; chosen.value = []; anchor.value = null;
});
watch(() => props.files, () => { active.value = null; chosen.value = []; anchor.value = null; });
</script>
<template>
  <aside class="file-changes-list" :aria-label="i18n.t('changesFiles')" :style="{ gridTemplateRows: 'minmax(0, ' + layout.sizes.unstaged + '%) 8px minmax(0, 1fr)' }">
    <template v-for="group in groups" :key="group.area"><section>
      <header><h2>{{ i18n.t(group.label) }} <span>{{ group.files.length }}</span></h2><button class="move-files" :disabled="disabled || !paths(group.area, group.files).length" @click="emit('move', { operation: group.operation, paths: paths(group.area, group.files) })">{{ i18n.t(group.operation === 'stage' ? 'editStage' : 'editUnstage') }}</button></header>
      <p v-if="!group.files.length" class="empty-files">{{ i18n.t('changesGroupEmpty') }}</p>
      <ul v-else role="tree" aria-multiselectable="true" :aria-label="i18n.t(group.label)">
        <li v-for="row in group.rows" :key="row.key" role="treeitem" :aria-level="row.depth + 1" :aria-selected="keys(group.area).includes(row.key)" :aria-expanded="row.node.file ? undefined : !collapsed[group.area].includes(row.node.path)">
          <div class="tree-row" :class="{ selected: keys(group.area).includes(row.key) }" :style="{ paddingLeft: row.depth + 'rem' }">
            <button v-if="!row.node.file" class="folder-toggle" :disabled="disabled" :aria-label="i18n.t('localToggleFolder') + ': ' + row.node.path" :aria-expanded="!collapsed[group.area].includes(row.node.path)" @click.stop="toggle(row.node.path, group.area)"><MaterialIcon :name="collapsed[group.area].includes(row.node.path) ? 'chevron_right' : 'expand_more'" /></button>
            <span v-else class="tree-spacer" />
            <button :class="row.node.file ? 'file-change' : 'folder-select'" :disabled="disabled" :title="row.node.path" :aria-pressed="keys(group.area).includes(row.key)" @click="choose(row, group.area, $event)" @contextmenu.prevent="context(row, group.area, $event)" @keydown="keyboard(row, group.area, $event)">
              <template v-if="row.node.file"><FileStatusBadge :status="group.area === 'staged' ? row.node.file.indexStatus : row.node.file.workTreeStatus" /><MaterialIcon class="file-icon" name="description" /></template>
              <MaterialIcon v-else class="folder-icon" :name="collapsed[group.area].includes(row.node.path) ? 'folder' : 'folder_open'" />
              <span class="file-name">{{ row.node.name }}<small v-if="row.node.file?.originalPath">← {{ row.node.file.originalPath }}</small></span>
            </button>
          </div>
        </li>
      </ul>
    </section><ResizeHandle v-if="group.area === 'working'" v-model="layout.sizes.unstaged" axis="y" percent :min="15" :max="85" :label="i18n.t('resizeStaged')" /></template>
  </aside>
</template>
<style scoped>
.file-changes-list { min-width: 0; min-height: 0; background: var(--gc-surface); border: 1px solid var(--gc-border); border-radius: .5rem; display: grid; height: 100%; overflow: hidden; font-size: .8rem; }
section { min-height: 0; overflow: auto; }
header { display: flex; align-items: center; justify-content: space-between; position: sticky; top: 0; z-index: 1; background: var(--gc-surface-raised); border-bottom: 1px solid var(--gc-divider); padding: .35rem .5rem; }
button { color: var(--gc-text); background: transparent; border: 0; text-align: left; font: inherit; cursor: pointer; }
.move-files { padding: .25rem .5rem; background: var(--gc-surface); border: 1px solid var(--gc-border); border-radius: .25rem; }
h2 { display: flex; gap: .5rem; margin: 0; font-size: .85rem; }
h2 span, .empty-files, small { color: var(--gc-text-muted); }
.empty-files { padding: 1rem; }
ul { margin: 0; padding: 0 0 .5rem; list-style: none; }
.tree-row { display: flex; min-height: 28px; user-select: none; }
.tree-row:hover { background: var(--gc-surface-raised); }
.tree-row.selected { background: var(--gc-selection-bg); box-shadow: inset 3px 0 var(--gc-violet); }
.folder-toggle, .tree-spacer { flex: 0 0 1.35rem; padding: 0; }
.file-change, .folder-select { display: flex; align-items: center; gap: .35rem; min-width: 0; flex: 1; padding: .2rem .35rem; }
button:focus-visible { outline: 2px solid var(--gc-violet); outline-offset: -2px; }
.file-name { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.folder-icon { color: var(--gc-warning); }
.file-icon { color: var(--gc-text-muted); }
small { display: block; font-size: .7rem; }
</style>
