<script setup lang="ts">
import { computed } from 'vue';
import type { GitFileChange, DiffSelection } from '../../../core/repositories/models/repository-changes.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import ChangeTreeNode from './ChangeTreeNode.vue';
import ResizeHandle from '../../../shared/components/resize-handle/ResizeHandle.vue';
import { usePanelLayout } from '../../../core/preferences/panel-layout.store';
import { buildFileTree } from './file-tree';

const props = withDefaults(defineProps<{ files: readonly GitFileChange[]; selected: DiffSelection | null; disabled?: boolean }>(), { disabled: false });
defineEmits<{ select: [selection: DiffSelection]; move: [value: { operation: 'stage' | 'unstage'; paths: string[] }] }>();
const i18n = useLanguageStore();
const layout = usePanelLayout();
const groups = computed(() => [
  { area: 'working' as const, label: 'editUnstaged' as const, operation: 'stage' as const, files: props.files.filter(file => file.workTreeStatus !== '.') },
  { area: 'staged' as const, label: 'editStaged' as const, operation: 'unstage' as const, files: props.files.filter(file => file.indexStatus !== '.' && !file.isConflicted) },
]);
function paths(area: string, files: readonly GitFileChange[]) {
  return files.filter(file => !file.isConflicted && (props.selected?.area !== area || file.path === props.selected.path)).map(file => file.path);
}
</script>

<template>
  <aside class="file-changes-list" :aria-label="i18n.t('changesFiles')" :style="{ gridTemplateRows: 'minmax(0, ' + layout.sizes.unstaged + '%) 8px minmax(0, 1fr)' }">
    <template v-for="group in groups" :key="group.area"><section>
      <header><h2>{{ i18n.t(group.label) }} <span>{{ group.files.length }}</span></h2><button class="move-files" type="button" :disabled="disabled || paths(group.area, group.files).length === 0" @click="$emit('move', { operation: group.operation, paths: paths(group.area, group.files) })">{{ i18n.t(group.operation === 'stage' ? 'editStage' : 'editUnstage') }}</button></header>
      <p v-if="group.files.length === 0" class="empty-files">{{ i18n.t('changesGroupEmpty') }}</p>
      <ul v-else>
        <ChangeTreeNode v-for="node in buildFileTree(group.files)" :key="(node.file ? 'file:' : 'folder:') + node.path" :node="node" :area="group.area" :selected="selected" :disabled="disabled" @select="$emit('select', $event)" />
      </ul>
    </section>
    <ResizeHandle v-if="group.area === 'working'" v-model="layout.sizes.unstaged" axis="y" percent :min="15" :max="85" :label="i18n.t('resizeStaged')" />
    </template>
  </aside>
</template>

<style scoped>
.file-changes-list { min-width: 0; min-height: 0; background: var(--gc-surface); border: 1px solid var(--gc-border); border-radius: .5rem; display: grid; grid-template-rows: minmax(0, 1fr) minmax(0, 1fr); height: 100%; overflow: hidden; font-size: .8rem; }
section { min-height: 0; overflow: auto; }
header { display: flex; align-items: center; justify-content: space-between; position: sticky; top: 0; z-index: 1; background: var(--gc-surface-raised); border-bottom: 1px solid var(--gc-divider); padding: .35rem .5rem; }
.move-files { padding: .25rem .5rem; color: var(--gc-text); background: var(--gc-surface); border: 1px solid var(--gc-border); border-radius: .25rem; cursor: pointer; }
section + section { border-top: 1px solid var(--gc-border); }
h2 { display: flex; gap: .5rem; margin: 0; font-size: .85rem; }
h2 span, .empty-files, small { color: var(--gc-text-muted); }
.empty-files { padding: 0 1rem 1rem; font-size: .9rem; }
ul { margin: 0; padding: 0 0 .5rem; list-style: none; }
.file-change { width: 100%; display: flex; gap: .75rem; padding: .65rem 1rem; border: 0; text-align: left; color: var(--gc-text); background: transparent; font: inherit; cursor: pointer; }
.file-change:hover { background: var(--gc-surface-raised); }
.file-change[aria-pressed='true'] { background: var(--gc-selection-bg); box-shadow: inset 3px 0 var(--gc-violet); }
.file-change:focus-visible { outline: 2px solid var(--gc-violet); outline-offset: -2px; }
.file-name { min-width: 0; overflow-wrap: anywhere; }
small { display: block; font-size: .8rem; margin-top: .25rem; }
</style>
