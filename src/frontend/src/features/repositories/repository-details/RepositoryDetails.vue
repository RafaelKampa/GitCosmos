<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import Button from 'primevue/button';
import { RepositoryChangesApiService } from '../../../core/repositories/repository-changes-api.service';
import type { RepositoryChanges, DiffSelection, FileDiff } from '../../../core/repositories/models/repository-changes.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import FileChangesList from '../file-changes-list/FileChangesList.vue';
import DiffViewer from '../diff-viewer/DiffViewer.vue';

const props = defineProps<{ workspaceId: number; repositoryId: number }>();
const i18n = useLanguageStore();
const api = new RepositoryChangesApiService(() => i18n.language, i18n.t);
const repository = ref<RepositoryChanges | null>(null);
const selection = ref<DiffSelection | null>(null);
const diff = ref<FileDiff | null>(null);
const loading = ref(true); const diffLoading = ref(false);
const error = ref(''); const diffError = ref('');
let listRequest: AbortController | undefined;
let diffRequest: AbortController | undefined;
function message(cause: unknown) { return cause instanceof ApiError ? cause.message : i18n.t('changesError'); }
async function load() {
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
onUnmounted(() => { listRequest?.abort(); diffRequest?.abort(); });
</script>

<template>
  <section class="repository-details" :aria-busy="loading">
    <RouterLink :to="'/workspaces/' + workspaceId">{{ i18n.t('changesBack') }}</RouterLink>
    <div class="repository-heading">
      <div>
        <h1>{{ repository ? repository.name : i18n.t('changesTitle') }}</h1>
        <template v-if="repository">
          <code class="repository-path">{{ repository.localPath }}</code>
          <p>{{ i18n.t('gitBranch') }}: <strong>{{ repository.status.isDetached ? i18n.t('gitDetached') : repository.status.branch }}</strong>
            <span v-if="!repository.status.hasCommits"> · {{ i18n.t('gitNoCommits') }}</span></p>
        </template>
      </div>
      <Button :label="i18n.t('dashboardRefresh')" :disabled="loading" @click="load" />
    </div>
    <p v-if="loading" role="status">{{ i18n.t('changesLoading') }}</p>
    <p v-else-if="error" role="alert">{{ error }}</p>
    <template v-else-if="repository">
      <p v-if="repository.files.length === 0" class="repository-clean" role="status">{{ i18n.t('changesClean') }}</p>
      <div v-else class="repository-workbench">
        <FileChangesList :files="repository.files" :selected="selection" @select="select" />
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
          <p v-else class="diff-feedback">{{ i18n.t('diffSelectFile') }}</p>
        </section>
      </div>
    </template>
  </section>
</template>

<style scoped>
:global(main:has(.repository-details)) { max-width: 1600px; padding-block: 2rem; }
.repository-heading { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; margin-block: 1rem; }
.repository-heading h1 { margin: .5rem 0; overflow-wrap: anywhere; font-size: clamp(1.5rem, 2vw, 2rem); line-height: 1.2; }
.repository-path { color: var(--gc-text-muted); overflow-wrap: anywhere; }
.repository-workbench { display: grid; grid-template-columns: minmax(14rem, 20rem) minmax(0, 1fr); gap: 1rem; align-items: start; }
.file-diff-panel { min-width: 0; border: 1px solid var(--gc-border); border-radius: .5rem; overflow: hidden; background: var(--gc-surface); }
.file-diff-heading { padding: 1rem; border-bottom: 1px solid var(--gc-border); }
.file-diff-heading h2 { font-size: 1rem; overflow-wrap: anywhere; margin-bottom: .5rem; }
.file-diff-heading span { color: var(--gc-text-muted); font-size: .85rem; }
.diff-feedback { padding: 1rem; }
.repository-clean { padding: 1rem; color: var(--gc-success); background: var(--gc-success-bg); border-radius: .5rem; }
@media (max-width: 800px) { .repository-workbench { grid-template-columns: minmax(0, 1fr); } }
</style>
