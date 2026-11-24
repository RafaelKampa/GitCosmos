<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import Button from 'primevue/button';
import { RepositoryChangesApiService } from '../../../core/repositories/repository-changes-api.service';
import type { RepositoryChanges, DiffSelection, FileDiff } from '../../../core/repositories/models/repository-changes.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import FileChangesList from '../file-changes-list/FileChangesList.vue';
import DiffViewer from '../diff-viewer/DiffViewer.vue';
import RepositoryHistory from '../repository-history/RepositoryHistory.vue';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';
import RepositoryCommitForm from '../repository-commit-form/RepositoryCommitForm.vue';
import type { MoveFilesRequest, RepositoryEditResult } from '../../../core/repositories/models/repository-edit.model';

const props = defineProps<{ workspaceId: number; repositoryId: number }>();
const i18n = useLanguageStore();
const api = new RepositoryChangesApiService(() => i18n.language, i18n.t);
const repository = ref<RepositoryChanges | null>(null);
const selection = ref<DiffSelection | null>(null);
const diff = ref<FileDiff | null>(null);
const preferences = useExplorerPreferences();
const tab = computed({ get: () => preferences.repositoryTabs[props.repositoryId] === 'history' ? 'history' : 'changes', set: (value: string) => { preferences.repositoryTabs[props.repositoryId] = value; } });
const tabs = { changes: 'repositoryChangesTab', history: 'repositoryHistoryTab' } as const;
const loading = ref(true); const diffLoading = ref(false);
const operationBusy = ref(false); const refreshingEdit = ref(false); const editing = computed(() => operationBusy.value || refreshingEdit.value);
const editMessage = ref(''); const editWarning = ref('');
const commitForm = ref<InstanceType<typeof RepositoryCommitForm>>();
const error = ref(''); const diffError = ref('');
let listRequest: AbortController | undefined;
let diffRequest: AbortController | undefined;
function message(cause: unknown) { return cause instanceof ApiError ? cause.message : i18n.t('changesError'); }
function move(value: MoveFilesRequest) { void commitForm.value?.move(value); }
async function edited(result: RepositoryEditResult, text: string) {
  editMessage.value = text; editWarning.value = result.warning ?? ''; refreshingEdit.value = true;
  listRequest?.abort(); diffRequest?.abort(); selection.value = null; diff.value = null; diffError.value = ''; diffLoading.value = false;
  const current = new AbortController(); listRequest = current;
  try {
    const response = await api.getChanges(props.workspaceId, props.repositoryId, current.signal);
    if (!current.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('changesError'));
      repository.value = response.result;
    }
  } catch (cause: unknown) { if (!current.signal.aborted) editWarning.value = message(cause); }
  finally { if (listRequest === current) refreshingEdit.value = false; }
}
async function load() {
  operationBusy.value = false; refreshingEdit.value = false;
  editMessage.value = ''; editWarning.value = '';
  listRequest?.abort(); diffRequest?.abort();
  const current = new AbortController(); listRequest = current;
  loading.value = true; diffLoading.value = false; error.value = ''; diffError.value = '';
  repository.value = null; selection.value = null; diff.value = null;
  try {
    const response = await api.getChanges(props.workspaceId, props.repositoryId, current.signal);
    if (!current.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('changesError'));
      repository.value = response.result;
    }
  } catch (cause: unknown) { if (!current.signal.aborted) error.value = message(cause); }
  finally { if (!current.signal.aborted) loading.value = false; }
}
async function select(value: DiffSelection) {
  diffRequest?.abort();
  const current = new AbortController(); diffRequest = current;
  selection.value = value; diff.value = null; diffError.value = ''; diffLoading.value = true;
  try {
    const response = await api.getDiff(props.workspaceId, props.repositoryId, value, current.signal);
    if (!current.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('changesError'));
      diff.value = response.result;
    }
  } catch (cause: unknown) { if (!current.signal.aborted) diffError.value = message(cause); }
  finally { if (!current.signal.aborted) diffLoading.value = false; }
}
onMounted(load);
watch([() => props.workspaceId, () => props.repositoryId, () => i18n.language], load);
watch(() => preferences.repositoryVersions[props.repositoryId], load);
onUnmounted(() => { listRequest?.abort(); diffRequest?.abort(); });
</script>

<template>
  <section class="repository-details" :aria-busy="loading">
    <RouterLink :to="workspaceId ? '/workspaces/' + workspaceId : '/repositories'">{{ i18n.t('changesBack') }}</RouterLink>
    <div class="repository-heading">
      <div>
        <h1>{{ repository ? repository.name : i18n.t('changesTitle') }}</h1>
        <template v-if="repository">
          <code class="repository-path">{{ repository.localPath }}</code>
          <p>{{ i18n.t('gitBranch') }}: <strong>{{ repository.status.isDetached ? i18n.t('gitDetached') : repository.status.branch }}</strong>
            <span v-if="!repository.status.hasCommits"> · {{ i18n.t('gitNoCommits') }}</span></p>
        </template>
      </div>
      <div class="page-actions"><Button :label="i18n.t('dashboardRefresh')" :disabled="loading || editing" @click="load" /><div class="bulk-actions" :title="i18n.t('operationsUpcoming')"><button disabled>Fetch</button><button disabled>Pull</button><button disabled>Push</button></div></div>
    </div>
    <p v-if="editWarning" class="edit-warning" role="alert">{{ editWarning }}</p><p v-else-if="editMessage" role="status">{{ editMessage }}</p>
    <p v-if="loading" role="status">{{ i18n.t('changesLoading') }}</p>
    <p v-else-if="error" role="alert">{{ error }}</p>
    <template v-else-if="repository">
      <dl class="repository-metrics"><div class="metric-tile ahead"><dt>{{ i18n.t('gitChangedFiles') }}</dt><dd>{{ repository.status.changedFiles }}</dd></div><div class="metric-tile ahead"><dt>{{ i18n.t('aheadMetric') }}</dt><dd>{{ repository.status.ahead ?? '—' }}</dd></div><div class="metric-tile behind"><dt>{{ i18n.t('behindMetric') }}</dt><dd>{{ repository.status.behind ?? '—' }}</dd></div></dl>
      <nav class="repository-tabs" :aria-label="i18n.t('repositorySections')"><button v-for="(label, key) in tabs" :key="key" type="button" :disabled="editing" :aria-pressed="tab === key" @click="tab = key">{{ i18n.t(label) }}</button></nav>
      <RepositoryHistory v-if="tab === 'history'" :workspace-id="workspaceId" :repository-id="repositoryId" />
      <div v-else class="repository-workbench">
        <FileChangesList :files="repository.files" :selected="selection" :disabled="editing" @select="select" @move="move" />
        <section class="file-diff-panel" :aria-busy="diffLoading" :aria-label="i18n.t('changesDiff')">
          <div v-if="selection" class="file-diff-heading">
            <h2>{{ selection.path }}</h2>
            <span>{{ i18n.t(selection.area === 'staged' ? 'changesStaged' : 'changesWorking') }}</span>
          </div>
          <p v-if="diffLoading" class="diff-feedback" role="status">{{ i18n.t('diffLoading') }}</p>
          <div v-else-if="diffError" class="diff-feedback">
            <p role="alert">{{ diffError }}</p>
            <Button :label="i18n.t('diffRetry')" @click="select(selection!)" />
          </div>
          <DiffViewer v-else-if="diff" :diff="diff" />
          <p v-else-if="repository.files.length === 0" class="repository-clean" role="status">{{ i18n.t('changesClean') }}</p>
          <p v-else class="diff-feedback">{{ i18n.t('diffSelectFile') }}</p>
        </section>
        <div class="repository-commit-panel">
          <RepositoryCommitForm ref="commitForm" :repository-id="repositoryId" :workspace-id="workspaceId" :files="repository.files" :has-commits="repository.status.hasCommits" @busy="operationBusy = $event" @changed="edited" />
        </div>
      </div>
    </template>
  </section>
</template>

<style scoped>
.repository-heading { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; margin-block: 1rem; }
.repository-heading h1 { margin: .5rem 0; overflow-wrap: anywhere; font-size: clamp(1.5rem, 2vw, 2rem); line-height: 1.2; }
.repository-path { color: var(--gc-text-muted); overflow-wrap: anywhere; }
.repository-workbench { display: grid; grid-template-columns: minmax(12rem, 30%) minmax(0, 1fr); grid-template-rows: 100dvh auto; gap: .5rem; }
.repository-commit-panel { grid-column: 1 / -1; min-width: 0; border: 1px solid var(--gc-border); border-radius: .5rem; }
.repository-commit-panel :deep(.repository-commit-form) { border-top: 0; }
.file-diff-panel :deep(.diff-viewer) { flex: 1; min-height: 0; max-height: none; }
.edit-warning { color: var(--gc-warning); }
.file-diff-panel { min-width: 0; min-height: 0; display: flex; flex-direction: column; border: 1px solid var(--gc-border); border-radius: .5rem; overflow: auto; background: var(--gc-surface); }
.file-diff-heading { flex-shrink: 0; padding: 1rem; border-bottom: 1px solid var(--gc-border); }
.file-diff-heading h2 { font-size: 1rem; overflow-wrap: anywhere; margin-bottom: .5rem; }
.file-diff-heading span { color: var(--gc-text-muted); font-size: .85rem; }
.diff-feedback { padding: 1rem; }
.repository-clean { padding: 1rem; color: var(--gc-success); background: var(--gc-success-bg); border-radius: .5rem; }
@media (max-width: 800px) { .repository-workbench { grid-template-columns: minmax(0, 1fr); grid-template-rows: 100dvh 100dvh auto; } }
</style>
