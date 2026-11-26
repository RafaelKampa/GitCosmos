<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { CommitExplorerApiService } from '../../../core/repositories/commit-explorer-api.service';
import { RepositoryHistoryApiService } from '../../../core/repositories/repository-history-api.service';
import type { CommitDetails, CommitDiff } from '../../../core/repositories/models/repository-history.model';
import type { CommitFileContent, CommitTreeEntry } from '../../../core/repositories/models/commit-explorer.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import { buildFileTree } from '../file-changes-list/file-tree';
import CommitTreeNode from './CommitTreeNode.vue';
import DiffViewer from '../diff-viewer/DiffViewer.vue';
import ResizeHandle from '../../../shared/components/resize-handle/ResizeHandle.vue';
import { usePanelLayout } from '../../../core/preferences/panel-layout.store';
const props = defineProps<{ repositoryId: number; workspaceId: number; details: CommitDetails; mode: 'changes' | 'tree'; focusPath: string }>();
defineEmits<{ showTree: [path: string] }>();
const i18n = useLanguageStore(); const api = new CommitExplorerApiService(() => i18n.language, i18n.t); const history = new RepositoryHistoryApiService(() => i18n.language, i18n.t);
const layout = usePanelLayout();
const entries = ref<CommitTreeEntry[]>([]); const selected = ref(''); const search = ref(''); const diff = ref<CommitDiff | null>(null); const content = ref<CommitFileContent | null>(null);
const contentLines = computed(() => content.value?.text.split('\n') || []);
const loading = ref(false); const fileLoading = ref(false); const error = ref(''); const fileError = ref('');
let treeRequest: AbortController | undefined; let fileRequest: AbortController | undefined;
const files = computed(() => props.mode === 'changes' ? props.details.files.map(file => ({ path: file.path, status: file.status })) : entries.value.map(entry => ({ path: entry.path, status: '' })));
const tree = computed(() => buildFileTree(files.value.filter(file => file.path.toLocaleLowerCase().includes(search.value.toLocaleLowerCase())).map(file => ({ path: file.path, originalPath: null, indexStatus: file.status, workTreeStatus: '.', isUntracked: false, isConflicted: false, isSubmodule: false }))));
function failure(cause: unknown) { return cause instanceof ApiError ? cause.message : i18n.t('commitFileError'); }
async function select(path: string) {
  fileRequest?.abort(); const current = new AbortController(); fileRequest = current; selected.value = path; diff.value = null; content.value = null; fileLoading.value = true; fileError.value = '';
  try {
    if (props.mode === 'changes') { const result = await history.getDiff(props.workspaceId, props.repositoryId, props.details.commit.sha, path, current.signal); if (!current.signal.aborted) { if (!result.result) throw new Error(); diff.value = result.result; } }
    else { const result = await api.file({ repositoryId: props.repositoryId, workspaceId: props.workspaceId, sha: props.details.commit.sha, path }, current.signal); if (!current.signal.aborted) { if (!result.result) throw new Error(); content.value = result.result; } }
  } catch (cause) { if (!current.signal.aborted) fileError.value = failure(cause); }
  finally { if (!current.signal.aborted) fileLoading.value = false; }
}
async function load() {
  treeRequest?.abort(); fileRequest?.abort(); const current = new AbortController(); treeRequest = current; selected.value = ''; content.value = null; diff.value = null; loading.value = true; fileLoading.value = false; error.value = ''; fileError.value = ''; search.value = '';
  try {
    if (props.mode === 'tree') { const result = await api.tree({ repositoryId: props.repositoryId, workspaceId: props.workspaceId, sha: props.details.commit.sha, path: '' }, current.signal); if (current.signal.aborted) return; if (!result.result) throw new Error(); entries.value = result.result; }
    const path = files.value.find(file => file.path === props.focusPath)?.path || files.value[0]?.path;
    if (path) void select(path);
  } catch (cause) { if (!current.signal.aborted) error.value = failure(cause); }
  finally { if (!current.signal.aborted) loading.value = false; }
}
watch([() => props.details.commit.sha, () => props.repositoryId, () => props.workspaceId, () => props.mode, () => i18n.language], load, { immediate: true });
watch(() => props.focusPath, path => { if (files.value.some(file => file.path === path)) void select(path); });
onUnmounted(() => { treeRequest?.abort(); fileRequest?.abort(); });
</script>
<template>
  <div class="commit-file-browser" :style="{ '--commit-tree-width': layout.sizes.commitTreeWidth + '%' }">
    <aside class="commit-snapshot-tree"><label>{{ i18n.t('commitFindFile') }}<input v-model="search" type="search" /></label><p v-if="loading" role="status">{{ i18n.t('historyLoading') }}</p><div v-else-if="error"><p role="alert">{{ error }}</p><button @click="load">{{ i18n.t('diffRetry') }}</button></div><ul v-else><CommitTreeNode v-for="node in tree" :key="node.path" :node="node" :selected="selected" :repository-id="repositoryId" :workspace-id="workspaceId" :sha="details.commit.sha" :parent="details.commit.parents[0] || ''" :changed="mode === 'changes'" @select="select" @show-tree="$emit('showTree', $event)" /></ul></aside>
    <ResizeHandle v-model="layout.sizes.commitTreeWidth" class="commit-tree-resize" axis="x" percent :min="15" :max="70" :label="i18n.t('resizeChangesWidth')" />
    <section class="commit-snapshot-content"><h3 v-if="selected">{{ selected }}</h3><p v-if="fileLoading" role="status">{{ i18n.t('diffLoading') }}</p><div v-else-if="fileError"><p role="alert">{{ fileError }}</p><button @click="select(selected)">{{ i18n.t('diffRetry') }}</button></div><DiffViewer v-else-if="diff" :diff="diff" /><ol v-else-if="content?.kind === 'text'" class="snapshot-code"><li v-for="(line, index) in contentLines.slice(0, 4000)" :key="index"><code>{{ line }}</code></li></ol><p v-else-if="content">{{ i18n.t(content.kind === 'binary' ? 'diffBinary' : 'diffSubmodule') }}</p><p v-else>{{ i18n.t('diffSelectFile') }}</p><p v-if="contentLines.length > 4000">{{ i18n.t('commitPreviewLimit') }}</p></section>
  </div>
</template>
<style scoped>
.commit-file-browser { display: grid; grid-template-columns: minmax(0, var(--commit-tree-width)) 8px minmax(0, 1fr); min-height: 20rem; height: 100%; }
.commit-snapshot-tree, .commit-snapshot-content { min-width: 0; overflow: auto; }
.commit-snapshot-tree { background: var(--gc-surface); padding: .4rem; }
.commit-snapshot-tree ul { list-style: none; padding: 0; }
label { display: grid; gap: .3rem; font-size: .8rem; }
.commit-snapshot-content h3 { position: sticky; top: 0; margin: 0; padding: .4rem; background: var(--gc-surface-raised); font-size: .8rem; overflow-wrap: anywhere; }
.snapshot-code { min-width: max-content; margin: .5rem 0; padding-left: 4rem; font-size: .8rem; }
.snapshot-code li { padding-inline: .75rem; color: var(--gc-text-muted); }
.snapshot-code code { white-space: pre; color: var(--gc-text); }
.commit-snapshot-content :deep(.diff-viewer) { max-height: none; }
@media (max-width: 650px) { .commit-file-browser { grid-template-columns: minmax(0, 1fr); height: auto; } .commit-tree-resize { display: none; } .commit-snapshot-tree { max-height: 18rem; } }
</style>
