<script setup lang="ts">
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
      <button type="button" class="folder-toggle" :aria-expanded="expanded" @click="expanded = !expanded"><span aria-hidden="true">{{ expanded ? '⌄' : '›' }}</span><svg viewBox="0 0 16 16" aria-hidden="true"><path d="M1 3h6l2 2h6v9H1z" /></svg>{{ node.name }}</button>
      <ul v-if="expanded"><ChangeTreeNode v-for="child in node.children" :key="(child.file ? 'file:' : 'folder:') + child.path" :node="child" :area="area" :selected="selected" :disabled="disabled" @select="$emit('select', $event)" /></ul>
    </template>
    <button v-else type="button" class="file-change" :class="{ conflicted: node.file.isConflicted }" :disabled="disabled" :title="node.file.path" :aria-pressed="selected?.path === node.file.path && selected.area === area" @click="$emit('select', { path: node.file.path, area })">
      <FileStatusBadge class="change-code" :status="area === 'staged' ? node.file.indexStatus : node.file.workTreeStatus" />
      <svg class="file-icon" viewBox="0 0 16 16" aria-hidden="true"><path d="M3 1h7l3 3v11H3zM9 1v4h4" /></svg>
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
svg { width: 14px; height: 14px; flex: 0 0 14px; }
.folder-toggle svg { fill: var(--gc-warning); }
.file-icon { fill: none; stroke: var(--gc-text-muted); stroke-width: 1.2; }
.file-change > span:last-child { min-width: 0; overflow-wrap: anywhere; }
small { display: block; color: var(--gc-text-muted); font-size: .7rem; }
</style>
