<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';
import { ApiError } from '../../../core/api/api-error';
import { RepositoryBranchApiService } from '../../../core/repositories/repository-branch-api.service';
import type { BranchOperation, BranchItem, BranchResources, LocalGitBranches } from '../../../core/repositories/models/repository-branch.model';
import BranchContextMenu from './BranchContextMenu.vue';
import BranchActionDialog from './BranchActionDialog.vue';
const props = withDefaults(defineProps<{ repositoryId: number; workspaceId: number; remoteMode?: boolean }>(), { remoteMode: false });
const i18n = useLanguageStore(); const preferences = useExplorerPreferences(); const router = useRouter();
const api = new RepositoryBranchApiService(() => i18n.language, i18n.t);
const snapshot = ref<LocalGitBranches | null>(null); const resources = ref<BranchResources>({ currentBranch: '', operationState: '', remotes: [], branches: [] });
const loading = ref(true); const busy = ref(false); const error = ref(''); const success = ref(''); const branchSearch = ref(''); const selected = ref('');
const action = ref<{ branch: BranchItem; operation: BranchOperation } | null>(null);
let readController: AbortController | undefined; let resourceController: AbortController | undefined;
const currentBranch = computed(() => resources.value.currentBranch || snapshot.value?.currentBranch || '');
const items = computed<BranchItem[]>(() => props.remoteMode ? resources.value.branches.map(branch => ({ name: branch.name, sha: branch.sha, upstream: null, isCurrent: false, isCheckedOut: false, isRemote: true, remote: branch.remote, sourceBranch: branch.branch }))
  : (snapshot.value?.branches || []).map(branch => {
    const tracking = [...resources.value.remotes].sort((a, b) => b.length - a.length).find(name => branch.upstream?.startsWith('refs/remotes/' + name + '/'));
    return { ...branch, isRemote: false, remote: tracking || '', sourceBranch: tracking ? branch.upstream!.slice(('refs/remotes/' + tracking + '/').length) : branch.name };
  }));
const visible = computed(() => items.value.filter(branch => branch.name.toLocaleLowerCase().includes(branchSearch.value.trim().toLocaleLowerCase())));
const groups = computed(() => props.remoteMode ? [...new Set(visible.value.map(branch => branch.remote))] : ['']);
function failure(cause: unknown) { return cause instanceof ApiError ? cause.message : i18n.t('branchError'); }
async function loadResources() {
  resourceController?.abort(); const current = new AbortController(); resourceController = current;
  error.value = '';
  try {
    const response = await api.getResources(props.repositoryId, props.workspaceId, current.signal);
    if (current.signal.aborted) return false;
    if (!response.result) throw new ApiError(i18n.t('branchError'));
    resources.value = response.result; return true;
  } catch (cause: unknown) { if (!current.signal.aborted) error.value = failure(cause); return false; }
}
async function load() {
  readController?.abort(); const current = new AbortController(); readController = current; loading.value = true; error.value = '';
  try {
    if (props.remoteMode) await loadResources();
    else {
      const response = await api.getBranches(props.repositoryId, props.workspaceId, current.signal);
      if (!current.signal.aborted) { if (!response.result) throw new ApiError(i18n.t('branchError')); snapshot.value = response.result; }
    }
  } catch (cause: unknown) { if (!current.signal.aborted) { error.value = failure(cause); snapshot.value = null; } }
  finally { if (!current.signal.aborted) loading.value = false; }
}
function openHistory(branch: BranchItem) {
  selected.value = branch.name;
  preferences.historyNavigation[props.repositoryId] = { branch: branch.name, sha: branch.sha, revision: (preferences.historyNavigation[props.repositoryId]?.revision || 0) + 1 };
  preferences.repositoryTabs[props.repositoryId] = 'history';
  void router.push(props.workspaceId ? `/workspaces/${props.workspaceId}/repositories/${props.repositoryId}` : `/repositories/${props.repositoryId}`);
}
async function choose(branch: BranchItem, operation: BranchOperation | 'copy') {
  if (busy.value) return;
  if (operation === 'copy') {
    try { await navigator.clipboard.writeText(branch.name); success.value = i18n.t('branchCopied'); }
    catch { error.value = i18n.t('branchCopyError'); }
    return;
  }
  if (!await loadResources()) return;
  action.value = { branch: items.value.find(item => item.name === branch.name && item.isRemote === branch.isRemote) || branch, operation };
}
function invalidate() { preferences.repositoryVersions[props.repositoryId] = (preferences.repositoryVersions[props.repositoryId] || 0) + 1; void loadResources(); }
function changed(message: string) { success.value = message; action.value = null; busy.value = false; invalidate(); }
function close() { action.value = null; busy.value = false; }
function repositoryAction(operation: BranchOperation) {
  const fallback = { name: currentBranch.value || resources.value.remotes[0] || 'repository', sha: '', upstream: null, isCurrent: false, isCheckedOut: false, isRemote: false, remote: resources.value.remotes[0] || '', sourceBranch: '' };
  const branch = operation === 'fetch' ? fallback : items.value.find(item => !item.isRemote && item.name === currentBranch.value) || items.value[0] || fallback;
  void choose(branch, operation);
}
function reset() { resourceController?.abort(); action.value = null; busy.value = false; branchSearch.value = ''; selected.value = ''; success.value = ''; snapshot.value = null; resources.value = { currentBranch: '', operationState: '', remotes: [], branches: [] }; void load(); }
onMounted(load);
watch([() => props.repositoryId, () => props.workspaceId, () => props.remoteMode, () => i18n.language], reset);
watch(() => preferences.repositoryVersions[props.repositoryId], load);
onUnmounted(() => { readController?.abort(); resourceController?.abort(); });
</script>
<template>
  <section class="local-branches" :aria-label="i18n.t(remoteMode ? 'repositoryRemotesTab' : 'repositoryBranchesTab')" :aria-busy="loading || busy">
    <div class="branch-toolbar"><button class="branch-refresh" type="button" :disabled="loading || busy" @click="load">{{ i18n.t('dashboardRefresh') }}</button><button v-if="remoteMode" class="branch-fetch" type="button" :disabled="loading || busy || !resources.remotes.length" @click="repositoryAction('fetch')">{{ i18n.t('branchFetch') }}</button></div>
    <p v-if="loading" role="status">{{ i18n.t('branchLoading') }}</p><p v-if="error" role="alert">{{ error }}</p><p v-if="success" role="status">{{ success }}</p>
    <template v-if="!loading">
      <label class="branch-search">{{ i18n.t('branchSearch') }}<input v-model="branchSearch" type="search" /></label>
      <p v-if="snapshot && !snapshot.hasCommits">{{ i18n.t('branchNoCommits') }}: {{ snapshot.currentBranch }}</p>
      <p v-if="snapshot?.isDetached">{{ i18n.t('gitDetached') }}</p>
      <div v-for="group in groups" :key="group" class="remote-branch-group"><h4 v-if="group">{{ group }}</h4><ul class="local-branch-list">
        <li v-for="branch in visible.filter(item => !remoteMode || item.remote === group)" :key="branch.name">
          <div class="branch-row"><button class="branch-select" :class="{ current: branch.isCurrent }" :title="branch.sha" :disabled="busy" :aria-pressed="selected === branch.name" @click="openHistory(branch)"><span aria-hidden="true">{{ branch.isCurrent ? '✓' : '⑂' }}</span> {{ branch.isRemote ? branch.sourceBranch : branch.name }}<span v-if="branch.isCurrent" class="branch-meta">({{ i18n.t('branchCurrent') }})</span></button>
          <BranchContextMenu :branch="branch" :current-branch="currentBranch" :disabled="busy" @open="loadResources" @select="choose(branch, $event)" /></div>
          <small v-if="branch.upstream" class="branch-meta">↳ {{ branch.upstream.replace(/^refs\/remotes\//, '') }}</small><small v-if="branch.isCheckedOut && !branch.isCurrent" class="branch-meta">{{ i18n.t('branchOtherWorktree') }}</small>
        </li>
      </ul></div>
      <p v-if="!visible.length">{{ i18n.t('branchNoResults') }}</p>
      <div v-if="resources.operationState === 'merge' || resources.operationState === 'rebase'" class="branch-recovery"><p>{{ i18n.t('branchPending') }}: {{ resources.operationState }}</p><button type="button" :disabled="busy" @click="repositoryAction('continue')">{{ i18n.t('branchContinue') }}</button><button type="button" :disabled="busy" @click="repositoryAction('abort')">{{ i18n.t('branchAbort') }}</button></div>
    </template>
    <BranchActionDialog v-if="action" :repository-id="repositoryId" :workspace-id="workspaceId" :branch="action.branch" :operation="action.operation" :resources="resources" @busy="busy = $event" @close="close" @changed="changed" @uncertain="invalidate" />
  </section>
</template>
<style scoped>
.local-branches { padding: .3rem .2rem .5rem var(--gc-tree-indent); font-size: .75rem; overflow-wrap: anywhere; }
.local-branches .local-branch-list { list-style: none; padding-left: var(--gc-tree-indent); margin: .4rem 0; border-left: 1px solid var(--gc-divider); }
.local-branch-list li { margin-bottom: .3rem; }
.branch-row { display: flex; min-width: 0; align-items: center; }
button { background: var(--gc-surface); color: var(--gc-text); border: 1px solid var(--gc-border); border-radius: .3rem; padding: .3rem .4rem; text-align: left; }
.branch-select { flex: 1; min-width: 0; border-color: transparent; background: transparent; overflow-wrap: anywhere; }
.branch-select[aria-pressed='true'] { background: var(--gc-selection-bg); border-color: var(--gc-violet); }
.current { font-weight: 650; }
.branch-meta { display: block; padding-left: .5rem; color: var(--gc-text-muted); }
.branch-search { display: grid; gap: .3rem; margin-block: .5rem; }
.branch-search input { width: 100%; padding: .35rem; }
.branch-toolbar, .branch-recovery { display: flex; flex-wrap: wrap; gap: .3rem; }
.branch-recovery p { flex-basis: 100%; }
.remote-branch-group h4 { margin: .7rem 0 .2rem; color: var(--gc-text-muted); }
</style>
