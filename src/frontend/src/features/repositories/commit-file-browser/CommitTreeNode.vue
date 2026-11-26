<script setup lang="ts">
import { ref } from 'vue';
import MaterialIcon from '../../../shared/components/material-icon/MaterialIcon.vue';
import FileStatusBadge from '../../../shared/components/file-status-badge/FileStatusBadge.vue';
import CommitFileMenu from '../commit-file-menu/CommitFileMenu.vue';
import type { FileTreeNode } from '../file-changes-list/file-tree';
defineProps<{ node: FileTreeNode; selected: string; repositoryId: number; workspaceId: number; sha: string; parent: string; changed: boolean }>();
defineEmits<{ select: [path: string]; showTree: [path: string] }>();
const expanded = ref(true);
</script>
<template>
  <li>
    <template v-if="!node.file"><button class="commit-folder" :aria-expanded="expanded" @click="expanded = !expanded"><MaterialIcon :name="expanded ? 'expand_more' : 'chevron_right'" /><MaterialIcon class="folder-icon" :name="expanded ? 'folder_open' : 'folder'" /><span>{{ node.name }}</span></button><ul v-if="expanded"><CommitTreeNode v-for="child in node.children" :key="child.path" :node="child" :selected="selected" :repository-id="repositoryId" :workspace-id="workspaceId" :sha="sha" :parent="parent" :changed="changed" @select="$emit('select', $event)" @show-tree="$emit('showTree', $event)" /></ul></template>
    <div v-else class="commit-tree-file"><button class="commit-tree-select" :data-path="node.path" :aria-pressed="selected === node.path" :title="node.path" @click="$emit('select', node.path)"><FileStatusBadge v-if="changed" :status="node.file.indexStatus" /><MaterialIcon name="description" /><span>{{ node.name }}</span></button><CommitFileMenu :repository-id="repositoryId" :workspace-id="workspaceId" :sha="sha" :path="node.path" :parent="parent" :changed="changed" :deleted="node.file.indexStatus.startsWith('D')" @show-tree="$emit('showTree', $event)" /></div>
  </li>
</template>
<style scoped>
ul { list-style: none; padding-left: 1rem; }
button { display: flex; align-items: center; gap: .25rem; min-width: 0; text-align: left; background: transparent; border: 0; color: var(--gc-text); padding: .25rem; }
button > span:last-child { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.commit-tree-file { display: flex; min-width: 0; align-items: center; }
.commit-tree-select { flex: 1; }
.commit-tree-select[aria-pressed='true'] { background: var(--gc-selection-bg); }
.folder-icon { color: var(--gc-warning); }
</style>
