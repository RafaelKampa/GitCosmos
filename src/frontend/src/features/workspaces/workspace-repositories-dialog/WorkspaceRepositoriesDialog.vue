<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import Dialog from 'primevue/dialog';
import FolderPathField from '../../../shared/components/folder-path-field/FolderPathField.vue';
import RepositoryCloneForm from '../repository-clone-form/RepositoryCloneForm.vue';
import Button from 'primevue/button';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { WorkspaceRepositoryApiService } from '../../../core/workspaces/workspace-repository-api.service';
import type { WorkspaceResponse } from '../../../core/workspaces/models/responses/workspace-response.model';
import type { WorkspaceRepositoryResponse } from '../../../core/workspaces/models/responses/workspace-repository-response.model';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
import type { RepositoryImportResponse } from '../../../core/workspaces/models/responses/repository-import-response.model';
import { ApiError } from '../../../core/api/api-error';

const props = defineProps<{ workspace: WorkspaceResponse }>();
const emit = defineEmits<{ close: []; changed: [] }>();
const i18n = useLanguageStore();
const api = new WorkspaceRepositoryApiService(() => i18n.language, i18n.t);
const repositories = ref<readonly WorkspaceRepositoryResponse[]>([]);
const path = ref('');
const importResult = ref<RepositoryImportResponse | null>(null);
const importLabels = { added: 'importAdded', duplicate: 'importDuplicate', ignored: 'importIgnored', failed: 'importFailed' } as const;
const loading = ref(true);
const loaded = ref(false);
const saving = ref(false);
const selecting = ref(false);
const mode = ref<'local' | 'clone'>('local');
const cloneBusy = ref(false);
const error = ref('');
const message = ref('');
const pendingRemoval = ref<WorkspaceRepositoryResponse | null>(null);
let listRequest: AbortController | undefined;
let saveRequest: AbortController | undefined;
function showError(cause: unknown) {
  error.value = cause instanceof ApiError ? cause.message : i18n.t('repositoryError');
}
async function load() {
  listRequest?.abort();
  const request = new AbortController();
  listRequest = request;
  loading.value = true;
  loaded.value = false;
  error.value = '';
  try {
    const response = await api.getRepositories(props.workspace.id, request.signal);
    if (!request.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('repositoryError'));
      repositories.value = response.result;
      loaded.value = true;
    }
  } catch (cause: unknown) {
    if (!request.signal.aborted) showError(cause);
  } finally {
    if (!request.signal.aborted) loading.value = false;
  }
}

async function mutate(operation: (signal: AbortSignal) => Promise<CustomResponse<unknown>>, onSuccess: () => void) {
  if (saving.value || selecting.value || cloneBusy.value) return;
  const request = new AbortController();
  saveRequest = request;
  saving.value = true;
  error.value = '';
  message.value = '';
  try {
    const response = await operation(request.signal);
    if (!request.signal.aborted) {
      message.value = response.messages.join(' ');
      onSuccess();
      emit('changed');
      await load();
    }
  } catch (cause: unknown) {
    if (!request.signal.aborted) showError(cause);
  } finally {
    if (!request.signal.aborted) saving.value = false;
  }
}

function add() {
  const localPath = path.value.trim();
  importResult.value = null;
  return mutate(async signal => {
    const response = await api.importRepositories({ workspaceId: props.workspace.id, localPath }, signal);
    if (!signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('repositoryError'));
      importResult.value = response.result;
    }
    return response;
  }, () => { /* Preserve the selected path for reviewing and retrying individual outcomes. */ });
}
function remove(repositoryId: number) {
  return mutate(signal => api.removeRepository(props.workspace.id, repositoryId, signal), () => { pendingRemoval.value = null; });
}
function close() { if (!saving.value && !selecting.value && !cloneBusy.value) emit('close'); }
function cloned() { emit('changed'); void load(); }
function setMode(value: 'local' | 'clone') { error.value = ''; message.value = ''; importResult.value = null; mode.value = value; }
function cancelRemoval() { if (!saving.value) pendingRemoval.value = null; }

onMounted(load);
watch(() => i18n.language, () => { message.value = ''; importResult.value = null; void load(); });
onUnmounted(() => { listRequest?.abort(); saveRequest?.abort(); });
</script>

<template>
  <Dialog :visible="true" modal :closable="!saving && !selecting && !cloneBusy" :close-on-escape="!saving && !selecting && !cloneBusy" :dismissable-mask="false"
    :header="i18n.t('workspaceRepositories') + ' — ' + workspace.name" class="workspace-action-dialog"
    :style="{ width: '52rem', maxWidth: 'calc(100vw - 2rem)' }" @update:visible="close">
    <section class="repository-manager" :aria-busy="loading || saving || selecting || cloneBusy">
      <div class="repository-add-options" role="group" :aria-label="i18n.t('repositoryAddOptions')">
        <button type="button" :aria-pressed="mode === 'local'" :disabled="saving || selecting || cloneBusy || pendingRemoval !== null" @click="setMode('local')">{{ i18n.t('addLocalRepository') }}</button>
        <button type="button" :aria-pressed="mode === 'clone'" :disabled="saving || selecting || cloneBusy || pendingRemoval !== null" @click="setMode('clone')">{{ i18n.t('cloneRepository') }}</button>
      </div>
      <form v-if="mode === 'local'" class="repository-path-form" @submit.prevent="add">
        <FolderPathField id="repository-path" v-model="path" :label="i18n.t('repositoryPath')" :help="i18n.t('importHelp')"
          :disabled="loading || saving || pendingRemoval !== null" @busy="selecting = $event" @error="error = $event" />
        <Button type="submit" :label="i18n.t('addRepository')" :disabled="loading || saving || selecting || pendingRemoval !== null" />
      </form>
      <RepositoryCloneForm v-else :workspace-id="workspace.id" :disabled="loading || saving || pendingRemoval !== null" @busy="cloneBusy = $event" @changed="cloned" />
      <p v-if="message" role="status">{{ message }}</p>
      <section v-if="importResult" class="repository-import-results" :aria-label="i18n.t('importResults')">
        <h3>{{ i18n.t('importResults') }}</h3>
        <p v-if="importResult.items.length === 0">{{ i18n.t('importEmpty') }}</p>
        <ul v-else>
          <li v-for="item in importResult.items" :key="item.localPath">
            <strong>{{ i18n.t(importLabels[item.status]) }}</strong> — <code>{{ item.localPath }}</code>
            <p>{{ item.message }}</p>
          </li>
        </ul>
      </section>
      <div v-if="error">
        <p role="alert">{{ error }}</p>
        <Button class="repository-retry" :label="i18n.t('repositoryRefresh')" :disabled="loading || saving || selecting || cloneBusy" @click="load" />
      </div>
      <p v-if="loading" role="status">{{ i18n.t('repositoriesLoading') }}</p>
      <template v-else-if="loaded">
        <p v-if="repositories.length === 0">{{ i18n.t('repositoriesEmpty') }}</p>
        <ul v-else class="repository-list">
          <li v-for="repository in repositories" :key="repository.id">
            <div><h3>{{ repository.name }}</h3><code>{{ repository.localPath }}</code></div>
            <button type="button" class="workspace-danger-action" :disabled="saving || selecting || cloneBusy || pendingRemoval !== null"
              @click="pendingRemoval = repository">{{ i18n.t('removeRepository') }}</button>
          </li>
        </ul>
      </template>
      <section v-if="pendingRemoval" class="repository-removal" :aria-label="i18n.t('confirmRemoveRepository')">
        <h3>{{ i18n.t('confirmRemoveRepository') }}</h3>
        <p class="repository-path"><strong>{{ pendingRemoval.name }}</strong> — {{ pendingRemoval.localPath }}</p>
        <p>{{ i18n.t('repositoryRemoveHint') }}</p>
        <div class="workspace-dialog-actions">
          <Button type="button" :label="i18n.t('cancel')" severity="secondary" :disabled="saving" @click="cancelRemoval" />
          <button type="button" class="workspace-danger-action" :disabled="saving"
            @click="remove(pendingRemoval.id)">{{ i18n.t('confirmRemoveRepository') }}</button>
        </div>
      </section>
      <div class="workspace-dialog-actions">
        <Button type="button" :label="i18n.t('closeRepositories')" severity="secondary" :disabled="saving || selecting || cloneBusy" @click="close" />
      </div>
    </section>
  </Dialog>
</template>

<style scoped>
.repository-manager, .repository-path-form { display: grid; gap: 1rem; }
.repository-import-results { padding: 1rem; border: 1px solid var(--gc-border); border-radius: .5rem; background: var(--gc-surface); }
.repository-import-results code { overflow-wrap: anywhere; }
.repository-add-options { display: flex; flex-wrap: wrap; gap: .75rem; }
.repository-add-options button { font: inherit; cursor: pointer; padding: .65rem .85rem; border-radius: .5rem; border: 1px solid var(--gc-border); background: var(--gc-surface); color: var(--gc-text); }
.repository-add-options button[aria-pressed='true'] { border-color: var(--gc-violet); background: var(--gc-selection-bg); }
.repository-add-options button:disabled { opacity: .6; cursor: default; }
.repository-path-form small, .repository-list code { color: var(--gc-text-muted); }
.repository-list { display: grid; gap: 1rem; padding: 0; list-style: none; }
.repository-list li { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; padding: 1rem; border: 1px solid var(--gc-divider); border-radius: .5rem; }
.repository-list li > div { min-width: 0; }
.repository-list h3 { margin: 0 0 .5rem; }
.repository-list code, .repository-path { overflow-wrap: anywhere; }
.repository-removal { border: 1px solid var(--gc-border); border-radius: .5rem; padding: 1rem; }
</style>
