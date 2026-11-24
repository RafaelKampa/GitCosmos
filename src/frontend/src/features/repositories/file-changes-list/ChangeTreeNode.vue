<script setup lang="ts">
import MaterialIcon from '../../../shared/components/material-icon/MaterialIcon.vue';
import { ref } from 'vue';
import type { FileTreeNode } from './file-tree';
import type { DiffArea, DiffSelection } from '../../../core/repositories/models/repository-changes.model';
import FileStatusBadge from '../../../shared/components/file-status-badge/FileStatusBadge.vue';
const props = defineProps<{ node: FileTreeNode; area: DiffArea; selected: DiffSelection | null; disabled: boolean }>();
defineEmits<{ select: [selection: DiffSelection] }>();
const expanded = ref(true);
</script>
<template>
  <li>
    <template v-if="!node.file">
      <button type="button" class="folder-toggle" :aria-expanded="expanded" @click="expanded = !expanded"><MaterialIcon :name="expanded ? 'expand_more' : 'chevron_right'" /><MaterialIcon class="folder-icon" :name="expanded ? 'folder_open' : 'folder'" />{{ node.name }}</button>
      <ul v-if="expanded"><ChangeTreeNode v-for="child in node.children" :key="(child.file ? 'file:' : 'folder:') + child.path" :node="child" :area="area" :selected="selected" :disabled="disabled" @select="$emit('select', $event)" /></ul>
    </template>
    <button v-else type="button" class="file-change" :class="{ conflicted: node.file.isConflicted }" :disabled="disabled" :title="node.file.path" :aria-pressed="selected?.path === node.file.path && selected.area === area" @click="$emit('select', { path: node.file.path, area })">
      <FileStatusBadge class="change-code" :status="area === 'staged' ? node.file.indexStatus : node.file.workTreeStatus" />
      <MaterialIcon class="file-icon" name="description" />
      <span>{{ node.name }}<small v-if="node.file.originalPath">← {{ node.file.originalPath }}</small></span>
    </button>
  </li>
</template>
<style scoped>
ul { list-style: none; margin: 0; padding-left: 1rem; }
button { display: flex; align-items: center; gap: .35rem; width: 100%; min-height: 28px; padding: .2rem .35rem; color: var(--gc-text); background: transparent; border: 0; text-align: left; font: inherit; cursor: pointer; }
button:hover { background: var(--gc-surface-raised); }
.file-change[aria-pressed='true'] { background: var(--gc-selection-bg); box-shadow: inset 3px 0 var(--gc-violet); }
button:focus-visible { outline: 2px solid var(--gc-violet); outline-offset: -2px; }

.folder-icon { color: var(--gc-warning); }
.file-icon { color: var(--gc-text-muted); }
.file-change > span:last-child { min-width: 0; overflow-wrap: anywhere; }
small { display: block; color: var(--gc-text-muted); font-size: .7rem; }
</style>
