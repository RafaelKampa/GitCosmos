<script setup lang="ts">
import MaterialIcon from '../../../shared/components/material-icon/MaterialIcon.vue';
import { computed, nextTick, onMounted, onUnmounted, ref, useId, watch } from 'vue';
import { RepositoryHistoryApiService } from '../../../core/repositories/repository-history-api.service';
import type { GitCommit, CommitDetails, CommitDiff } from '../../../core/repositories/models/repository-history.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import DiffViewer from '../diff-viewer/DiffViewer.vue';
import { buildCommitGraph } from './commit-graph';
import { formatCommitDate } from './history-presentation';
import CommitReferences from './CommitReferences.vue';
import AuthorAvatar from '../../../shared/components/author-avatar/AuthorAvatar.vue';
import HorizontalScroll from '../../../shared/components/horizontal-scroll/HorizontalScroll.vue';
import { foldMergeHistory } from './merge-folding';
import FileStatusBadge from '../../../shared/components/file-status-badge/FileStatusBadge.vue';
import HistoryFilters from './HistoryFilters.vue';
import CommitFileBrowser from '../commit-file-browser/CommitFileBrowser.vue';
import CommitFileMenu from '../commit-file-menu/CommitFileMenu.vue';
import ResizeHandle from '../../../shared/components/resize-handle/ResizeHandle.vue';
import { usePanelLayout } from '../../../core/preferences/panel-layout.store';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';

const props = defineProps<{ workspaceId: number; repositoryId: number }>();
const i18n = useLanguageStore();
const preferences = useExplorerPreferences();
const layout = usePanelLayout();
const detailTab = ref<'commit' | 'changes' | 'tree'>('commit'); const treeFocus = ref('');
function showTree(path: string) { treeFocus.value = path; detailTab.value = 'tree'; }
const filters = ref(preferences.historyFilters[props.repositoryId] || { branch: '', search: '' });
const historyList = ref<HTMLElement>(); const focusNotice = ref('');
let pendingFocus = preferences.historyNavigation[props.repositoryId]?.sha || ''; let focusVersion = 0;
const api = new RepositoryHistoryApiService(() => i18n.language, i18n.t);
const accordionId = useId();
const commits = ref<readonly GitCommit[]>([]);
const collapsedMerges = ref(new Set<string>());
const folded = computed(() => foldMergeHistory(commits.value, collapsedMerges.value));
const rowHeight = (commit: GitCommit) => commit.refs.trim() ? 56 : 32;
const expandedGraph = computed(() => buildCommitGraph(commits.value, rowHeight));
const graph = computed(() => buildCommitGraph(folded.value.graphCommits, rowHeight,
  new Map(commits.value.map((commit, index) => [commit.sha, expandedGraph.value[index]!.color]))));
const graphWidth = computed(() => Math.max(24, ...graph.value.map(row => row.width)));
const graphColor = (color: number) => `var(--gc-graph-${color % 8 + 1})`;
const snapshot = ref<string | null>(null);
const page = ref(0); const hasMore = ref(false); const loading = ref(true); const error = ref('');
const selected = ref(''); const details = ref<CommitDetails | null>(null);
const detailsLoading = ref(false); const detailsError = ref('');
const selectedFile = ref<string | null>(null); const diff = ref<CommitDiff | null>(null);
const diffLoading = ref(false); const diffError = ref('');
let listRequest: AbortController | undefined;
let detailsRequest: AbortController | undefined;
let diffRequest: AbortController | undefined;
function message(cause: unknown) { return cause instanceof ApiError ? cause.message : i18n.t('historyError'); }
function clearSelection() {
  detailsRequest?.abort(); diffRequest?.abort();
  selected.value = ''; details.value = null; detailsError.value = ''; detailsLoading.value = false;
  selectedFile.value = null; diff.value = null; diffError.value = ''; diffLoading.value = false;
}
async function load(reset = true) {
  if (!reset && loading.value) return;
  listRequest?.abort();
  if (reset) { focusVersion++; focusNotice.value = ''; pendingFocus = preferences.historyNavigation[props.repositoryId]?.sha || ''; commits.value = []; collapsedMerges.value = new Set(); snapshot.value = null; page.value = 0; hasMore.value = false; clearSelection(); }
  const current = new AbortController(); listRequest = current;
  loading.value = true; error.value = '';
  try {
    const response = filters.value.branch || filters.value.search
      ? await api.getHistory(props.workspaceId, props.repositoryId, page.value, snapshot.value, current.signal, filters.value)
      : await api.getHistory(props.workspaceId, props.repositoryId, page.value, snapshot.value, current.signal);
    if (!current.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('historyError'));
      commits.value = [...commits.value, ...response.result.commits];
      snapshot.value = response.result.snapshot; hasMore.value = response.result.hasMore; page.value++;
      if (reset && !pendingFocus && (filters.value.branch || filters.value.search) && commits.value.length) void selectCommit(commits.value[0]!.sha);
    }
  } catch (cause: unknown) { if (!current.signal.aborted) error.value = message(cause); }
  finally { if (!current.signal.aborted) { loading.value = false; if (pendingFocus) { const sha = pendingFocus; pendingFocus = ''; void focusCommit(sha); } } }
}
async function focusCommit(sha: string) {
  if (!/^(?:[a-f\d]{40}|[a-f\d]{64})$/i.test(sha)) return;
  const version = ++focusVersion; focusNotice.value = '';
  await selectCommit(sha, true);
  for (let attempts = 0; attempts < 200 && version === focusVersion && !commits.value.some(commit => commit.sha === sha) && hasMore.value && !loading.value && !error.value; attempts++) await load(false);
  if (version !== focusVersion) return;
  const expanded = new Set(collapsedMerges.value);
  for (const merge of collapsedMerges.value) if (foldMergeHistory(commits.value, new Set([merge])).hidden.has(sha)) expanded.delete(merge);
  if (expanded.size !== collapsedMerges.value.size) collapsedMerges.value = expanded;
  if (!commits.value.some(commit => commit.sha === sha)) focusNotice.value = i18n.t('historyFocusOutside');
  await nextTick(); historyList.value?.querySelector<HTMLElement>('[data-sha="' + sha + '"]')?.scrollIntoView?.({ block: 'nearest' });
}
async function selectCommit(sha: string, focused = false) {
  if (!focused) { focusVersion++; pendingFocus = ''; focusNotice.value = ''; }
  clearSelection(); selected.value = sha;
  const current = new AbortController(); detailsRequest = current; detailsLoading.value = true;
  try {
    const response = await api.getCommit(props.workspaceId, props.repositoryId, sha, current.signal);
    if (!current.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('historyError'));
      details.value = response.result;
    }
  } catch (cause: unknown) { if (!current.signal.aborted) detailsError.value = message(cause); }
  finally { if (!current.signal.aborted) detailsLoading.value = false; }
}
function toggleMerge(sha: string) {
  const next = new Set(collapsedMerges.value);
  if (next.has(sha)) next.delete(sha); else next.add(sha);
  collapsedMerges.value = next;
  if (folded.value.hidden.has(selected.value)) void selectCommit(sha);
}
function toggleFile(path: string) {
  if (selectedFile.value === path) {
    diffRequest?.abort(); selectedFile.value = null; diff.value = null; diffError.value = ''; diffLoading.value = false;
  } else { void selectFile(path); }
}
async function selectFile(path: string) {
  diffRequest?.abort();
  const current = new AbortController(); diffRequest = current;
  selectedFile.value = path; diff.value = null; diffError.value = ''; diffLoading.value = true;
  try {
    const response = await api.getDiff(props.workspaceId, props.repositoryId, selected.value, path, current.signal);
    if (!current.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('historyError'));
      diff.value = response.result;
    }
  } catch (cause: unknown) { if (!current.signal.aborted) diffError.value = message(cause); }
  finally { if (!current.signal.aborted) diffLoading.value = false; }
}
onMounted(load);
function applyFilters(value: { branch: string; search: string }) { delete preferences.historyNavigation[props.repositoryId]; pendingFocus = ''; filters.value = value; preferences.historyFilters[props.repositoryId] = value; void load(); }
watch([() => props.workspaceId, () => props.repositoryId, () => i18n.language], (value, old) => {
  if (value[0] !== old[0] || value[1] !== old[1]) filters.value = preferences.historyFilters[props.repositoryId] || { branch: '', search: '' };
  void load();
});
watch(() => preferences.historyNavigation[props.repositoryId], value => { if (value?.sha) { pendingFocus = value.sha; if (!loading.value) { pendingFocus = ''; void focusCommit(value.sha); } } });
onUnmounted(() => { focusVersion++; listRequest?.abort(); clearSelection(); });
</script>

<template>
  <section class="repository-history" :aria-label="i18n.t('repositoryHistoryTab')">
    <div class="history-toolbar"><p>{{ i18n.t('historyLocal') }}</p><button class="history-refresh" type="button" :disabled="loading" @click="load()">{{ i18n.t('dashboardRefresh') }}</button></div>
    <HistoryFilters :workspace-id="workspaceId" :repository-id="repositoryId" :branch="filters.branch" :search="filters.search" @apply="applyFilters" />
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="focusNotice" role="status">{{ focusNotice }}</p>
    <p v-if="loading" role="status">{{ i18n.t('historyLoading') }}</p>
    <p v-else-if="!error && commits.length === 0" role="status">{{ i18n.t(filters.branch || filters.search ? 'historyNoMatches' : 'historyEmpty') }}</p>
    <div class="history-workbench">
      <section ref="historyList" class="history-list" :aria-label="i18n.t('historyCommits')" :style="{ '--history-graph-width': graphWidth + 'px', height: layout.sizes.historyGraph ? layout.sizes.historyGraph + 'px' : undefined, maxHeight: layout.sizes.historyGraph ? 'none' : undefined }">
        <div class="history-columns" aria-hidden="true"><span /><span>{{ i18n.t('historySubject') }}</span><span>{{ i18n.t('historyAuthor') }}</span><span>{{ i18n.t('historyCommit') }}</span><span>{{ i18n.t('historyDate') }}</span></div>
        <div v-for="(commit, index) in folded.commits" :key="commit.sha" class="history-row" :data-sha="commit.sha" :class="{ selected: selected === commit.sha }" :style="{ height: graph[index]!.height + 'px' }">
          <span class="graph-cell">
            <svg :width="graphWidth" :height="graph[index]!.height" aria-hidden="true" class="commit-graph">
              <path v-for="(segment, edge) in graph[index]!.segments" :key="edge" :d="segment" :stroke="graphColor(graph[index]!.segmentColors[edge]!)" />
              <circle :cx="12 + graph[index]!.column * 18" :cy="graph[index]!.height / 2" :r="commit.parents.length > 1 ? 7 : 3" :stroke="graphColor(graph[index]!.color)" :fill="commit.parents.length > 1 ? 'var(--gc-surface)' : graphColor(graph[index]!.color)" />
              <path v-if="commit.parents.length > 1" class="merge-chevron" :d="collapsedMerges.has(commit.sha) ? `M ${10 + graph[index]!.column * 18} ${graph[index]!.height / 2 - 3} l 3 3 -3 3` : `M ${9 + graph[index]!.column * 18} ${graph[index]!.height / 2 - 1} l 3 3 3 -3`" :stroke="graphColor(graph[index]!.color)" />
            </svg>
            <button v-if="commit.parents.length > 1" type="button" class="merge-toggle" :style="{ left: graph[index]!.column * 18 + 'px', top: graph[index]!.height / 2 - 12 + 'px' }" :aria-expanded="!collapsedMerges.has(commit.sha)" :aria-label="i18n.t(collapsedMerges.has(commit.sha) ? 'historyExpandMerge' : 'historyCollapseMerge') + ': ' + commit.subject" :title="i18n.t(collapsedMerges.has(commit.sha) ? 'historyExpandMerge' : 'historyCollapseMerge')" @click="toggleMerge(commit.sha)" />
          </span>
          <span class="commit-summary">
            <HorizontalScroll :content-key="commit.subject"><button type="button" class="commit-choice" :aria-pressed="selected === commit.sha" :title="commit.subject" @click="selectCommit(commit.sha)"><strong>{{ commit.subject }}</strong></button></HorizontalScroll>
            <HorizontalScroll v-if="commit.refs.trim()" :content-key="commit.refs"><CommitReferences :value="commit.refs" /></HorizontalScroll>
          </span>
            <span class="commit-author" :title="commit.author + ' <' + commit.email + '>'"><AuthorAvatar :name="commit.author" :email="commit.email" /><span>{{ commit.author }}</span></span>
            <code class="commit-short-sha" :title="commit.sha">{{ commit.sha.slice(0, 8) }}</code>
            <time :datetime="commit.authoredAt" :title="commit.authoredAt">{{ formatCommitDate(commit.authoredAt, i18n.language) }}</time>
        </div>
        <button v-if="hasMore" class="history-more" type="button" :disabled="loading" @click="load(false)">{{ i18n.t('historyMore') }}</button>
        <button v-else-if="error" class="history-retry" type="button" :disabled="loading" @click="load(false)">{{ i18n.t('diffRetry') }}</button>
      </section>
      <ResizeHandle v-model="layout.sizes.historyGraph" axis="y" :min="120" :max="2000" :label="i18n.t('resizeHistoryGraph')" />
      <section class="commit-details" :class="{ 'commit-details--browser': detailTab !== 'commit' }" :aria-label="i18n.t('historyDetails')" :aria-busy="detailsLoading || diffLoading" :style="{ height: layout.sizes.historyDetails ? layout.sizes.historyDetails + 'px' : undefined }">
        <p v-if="detailsLoading" role="status">{{ i18n.t('historyLoading') }}</p>
        <div v-else-if="detailsError"><p role="alert">{{ detailsError }}</p><button class="commit-retry" type="button" @click="selectCommit(selected)">{{ i18n.t('diffRetry') }}</button></div>
        <template v-else-if="details">
          <nav class="commit-detail-tabs" :aria-label="i18n.t('commitDetailTabs')"><button v-for="item in (['commit', 'changes', 'tree'] as const)" :key="item" :aria-pressed="detailTab === item" @click="detailTab = item">{{ i18n.t(item === 'commit' ? 'commitTab' : item === 'changes' ? 'commitChangesTab' : 'commitTreeTab') }}</button></nav>
          <div v-if="detailTab !== 'commit'" class="commit-compact-summary"><AuthorAvatar :name="details.commit.author" :email="details.commit.email" /><strong>{{ details.commit.author }}</strong><code>{{ details.commit.sha.slice(0, 8) }}</code><time>{{ formatCommitDate(details.commit.authoredAt, i18n.language) }}</time><span>{{ details.commit.subject }}</span></div>
          <CommitFileBrowser v-if="detailTab !== 'commit'" :repository-id="repositoryId" :workspace-id="workspaceId" :details="details" :mode="detailTab === 'changes' ? 'changes' : 'tree'" :focus-path="treeFocus" @show-tree="showTree" />
          <template v-else>
          <div class="commit-author-details"><AuthorAvatar class="commit-large-avatar" :name="details.commit.author" :email="details.commit.email" /><div><small>{{ i18n.t('historyAuthor') }}</small><p><strong>{{ details.commit.author }}</strong> &lt;{{ details.commit.email }}&gt;</p><time>{{ formatCommitDate(details.commit.authoredAt, i18n.language) }}</time></div></div>
          <dl class="commit-metadata"><dt>SHA</dt><dd><code class="commit-sha">{{ details.commit.sha }}</code></dd><dt>{{ i18n.t('commitReferences') }}</dt><dd class="commit-detail-refs"><CommitReferences :value="details.commit.refs" /></dd><dt>{{ i18n.t('commitParents') }}</dt><dd class="commit-comparison"><span>{{ i18n.t(details.commit.parents.length ? 'historyFirstParent' : 'historyRoot') }}</span> <code v-for="parent in details.commit.parents" :key="parent">{{ parent }} </code></dd></dl>
          <h2 class="commit-subject">{{ details.commit.subject }}</h2>
          <pre class="commit-message">{{ details.commit.message }}</pre>
          <p v-if="details.files.length === 0">{{ i18n.t('historyNoFiles') }}</p>
          <ul class="commit-files"><li v-for="(file, index) in details.files" :key="file.path">
            <div class="commit-file-row"><button :id="accordionId + '-file-' + index" class="commit-file-toggle" type="button" :aria-expanded="selectedFile === file.path" :aria-controls="accordionId + '-diff-' + index" @click="toggleFile(file.path)">
              <MaterialIcon class="file-chevron" :name="selectedFile === file.path ? 'expand_more' : 'chevron_right'" /><FileStatusBadge :status="file.status" /> <span v-if="file.originalPath">{{ file.originalPath }} → </span>{{ file.path }}
            </button>
            <CommitFileMenu :repository-id="repositoryId" :workspace-id="workspaceId" :sha="details.commit.sha" :path="file.path" :parent="details.commit.parents[0] || ''" :deleted="file.status.startsWith('D')" @show-tree="showTree" /></div>
            <section v-if="selectedFile === file.path" :id="accordionId + '-diff-' + index" class="commit-file-diff" role="region" :aria-labelledby="accordionId + '-file-' + index" :aria-busy="diffLoading">
              <p v-if="diffLoading" role="status">{{ i18n.t('diffLoading') }}</p>
              <div v-else-if="diffError"><p role="alert">{{ diffError }}</p><button class="commit-diff-retry" type="button" @click="selectFile(file.path)">{{ i18n.t('diffRetry') }}</button></div>
              <DiffViewer v-else-if="diff" :diff="diff" />
            </section>
          </li></ul>
          </template>
        </template>
        <p v-else>{{ i18n.t('historySelect') }}</p>
      </section>
      <ResizeHandle v-model="layout.sizes.historyDetails" axis="y" :min="160" :max="2400" :label="i18n.t('resizeHistoryDetails')" />
    </div>
  </section>
</template>

<style scoped>
.history-toolbar { display: flex; gap: 1rem; justify-content: space-between; align-items: center; margin-block: .75rem; }
button { color: var(--gc-text); background: var(--gc-surface); border: 1px solid var(--gc-border); border-radius: .4rem; padding: .5rem .75rem; }
.history-toolbar p, .commit-comparison { color: var(--gc-text-muted); }
.history-workbench { display: grid; grid-template-columns: minmax(0, 1fr); }
.history-list, .commit-details { min-width: 0; border: 1px solid var(--gc-border); border-radius: .5rem; background: var(--gc-surface); overflow: auto; }
.history-list { max-height: 36vh; }
.history-columns, .history-row { min-width: calc(var(--history-graph-width) + 51rem); }
.history-columns { display: grid; grid-template-columns: var(--history-graph-width) minmax(20rem, 1fr) 12rem 6rem 13rem; align-items: center; height: 30px; position: sticky; top: 0; z-index: 1; background: var(--gc-surface-raised); color: var(--gc-text-muted); font-size: .75rem; border-bottom: 1px solid var(--gc-border); }
.history-columns > span { padding-inline: .5rem; }
.history-row { display: grid; grid-template-columns: var(--history-graph-width) minmax(20rem, 1fr) 12rem 6rem 13rem; align-items: center; font-size: .78rem; }
.history-row > * { min-width: 0; }
.history-row > :not(.graph-cell) { padding-inline: .5rem; }
.graph-cell { display: block; position: relative; align-self: stretch; }
.commit-graph { display: block; fill: none; stroke-width: 1.5; }
.merge-chevron { stroke-linecap: round; stroke-linejoin: round; }
.merge-toggle { position: absolute; width: 24px; height: 24px; border: 0; padding: 0; background: transparent; border-radius: 50%; }
.commit-choice { display: block; min-width: 100%; text-align: left; height: 24px; border: 0; border-radius: 0; padding: 0; background: transparent; font-size: inherit; white-space: nowrap; }
.commit-summary { display: grid; gap: 2px; min-width: 0; }
.commit-author > span:last-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.commit-author, .commit-author-details { display: flex; align-items: center; gap: .4rem; }
.history-row time { white-space: nowrap; color: var(--gc-text-muted); font-variant-numeric: tabular-nums; }
.commit-short-sha { color: var(--gc-text-muted); }
.history-row.selected, .commit-file-toggle[aria-expanded="true"] { background: var(--gc-selection-bg); outline: 1px solid var(--gc-violet); outline-offset: -1px; }
.history-row:hover { background: var(--gc-surface-raised); }
.commit-details { padding: 1rem; }
.commit-details--browser { display: flex; flex-direction: column; height: 60dvh; min-height: 0; }
.commit-details--browser > nav, .commit-compact-summary { flex-shrink: 0; }
.commit-details--browser > .commit-file-browser { flex: 1; min-height: 0; }
.commit-detail-tabs { display: flex; gap: .5rem; border-bottom: 1px solid var(--gc-border); margin: -1rem -1rem 1rem; padding: .25rem .75rem; }
.commit-detail-tabs button { border: 0; border-radius: 0; }
.commit-detail-tabs button[aria-pressed='true'] { color: var(--gc-primary); box-shadow: inset 0 -2px var(--gc-primary); }
.commit-author-details p { margin: .4rem 0; }
.commit-author-details .commit-large-avatar { width: 48px; height: 48px; flex-basis: 48px; font-size: 1rem; }
.commit-metadata { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: .4rem 1rem; font-size: .8rem; }
.commit-metadata dt { text-align: right; color: var(--gc-text-muted); }
.commit-metadata dd { margin: 0; overflow-wrap: anywhere; }
.commit-subject { border-block: 1px solid var(--gc-divider); padding-block: .8rem; }
.commit-compact-summary { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; padding-bottom: .5rem; font-size: .8rem; }
.commit-file-row { display: flex; min-width: 0; align-items: center; }
.commit-file-row .commit-file-toggle { flex: 1; min-width: 0; }
.commit-details h2 { font-size: 1.1rem; overflow-wrap: anywhere; }
.commit-sha, .commit-comparison, .commit-files { overflow-wrap: anywhere; }
.commit-message { white-space: pre-wrap; overflow-wrap: anywhere; font: inherit; }
.commit-files { list-style: none; padding: 0; }
.commit-detail-refs { overflow-x: auto; padding-block: .25rem; }
.commit-files > li { border: 1px solid var(--gc-divider); border-radius: .35rem; margin-block: .35rem; overflow: hidden; }
.commit-file-toggle { text-align: left; width: 100%; border: 0; border-radius: 0; overflow-wrap: anywhere; }
.file-chevron { display: inline-block; width: 1rem; }
.commit-file-diff { border-top: 1px solid var(--gc-divider); min-width: 0; }
.commit-file-diff > p, .commit-file-diff > div:not(.diff-viewer) { padding: .75rem; }
.history-more, .history-retry { margin: .75rem; }
@media (max-width: 1000px) { .history-list { max-height: 22.5vh; } }
</style>
