<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import Button from 'primevue/button';
import { RouterLink } from 'vue-router';
import WorkspaceCreateForm from '../workspace-create-form/WorkspaceCreateForm.vue';
import WorkspaceActionDialog from '../workspace-action-dialog/WorkspaceActionDialog.vue';
import WorkspaceRepositoriesDialog from '../workspace-repositories-dialog/WorkspaceRepositoriesDialog.vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { WorkspaceApiService } from '../../../core/workspaces/workspace-api.service';
import type { WorkspaceResponse } from '../../../core/workspaces/models/responses/workspace-response.model';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
import { ApiError } from '../../../core/api/api-error';

const i18n = useLanguageStore();
const api = new WorkspaceApiService(() => i18n.language, i18n.t);
const items = ref<readonly WorkspaceResponse[]>([]);
const page = ref(0);
const totalPages = ref(0);
const loading = ref(true);
const saving = ref(false);
const error = ref('');
const message = ref('');
const dialogError = ref('');
const action = ref<{ workspace: WorkspaceResponse; mode: 'rename' | 'delete' } | null>(null);
const repositoryWorkspace = ref<WorkspaceResponse | null>(null);
const formVersion = ref(0);
let listRequest: AbortController | undefined;
let saveRequest: AbortController | undefined;

function errorMessage(cause: unknown) {
  return cause instanceof ApiError ? cause.message : i18n.t('workspaceError');
}

async function load(targetPage = page.value) {
  listRequest?.abort();
  const request = new AbortController();
  listRequest = request;
  loading.value = true;
  error.value = '';
  try {
    const response = await api.getWorkspaces(targetPage, request.signal);
    if (!request.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('workspaceError'));
      items.value = response.result.items;
      page.value = response.result.page;
      totalPages.value = response.result.totalPages;
    }
  } catch (cause: unknown) {
    if (!request.signal.aborted) error.value = errorMessage(cause);
  } finally {
    if (!request.signal.aborted) loading.value = false;
  }
}

async function mutate(operation: (signal: AbortSignal) => Promise<CustomResponse<unknown>>, onSuccess: () => void) {
  if (saving.value) return;
  const request = new AbortController();
  saveRequest = request;
  saving.value = true;
  error.value = '';
  dialogError.value = '';
  message.value = '';
  try {
    const response = await operation(request.signal);
    if (!request.signal.aborted) {
      message.value = response.messages.join(' ');
      onSuccess();
      // Renaming can change the ordering; deleting can empty the current page.
      await load(0);
    }
  } catch (cause: unknown) {
    if (!request.signal.aborted) {
      if (action.value) dialogError.value = errorMessage(cause);
      else error.value = errorMessage(cause);
    }
  } finally {
    if (!request.signal.aborted) saving.value = false;
  }
}

function create(name: string) {
  return mutate(signal => api.createWorkspace({ name }, signal), () => { formVersion.value++; });
}
function rename(id: number, name: string) {
  return mutate(signal => api.updateWorkspace({ id, name }, signal), () => { action.value = null; });
}
function remove(id: number) {
  return mutate(signal => api.deleteWorkspace(id, signal), () => { action.value = null; });
}
function openAction(workspace: WorkspaceResponse, mode: 'rename' | 'delete') {
  dialogError.value = '';
  action.value = { workspace, mode };
}
function cancelAction() {
  if (!saving.value) action.value = null;
}

onMounted(() => load());
watch(() => i18n.language, () => { message.value = ''; void load(); });
onUnmounted(() => { listRequest?.abort(); saveRequest?.abort(); });
</script>

<template>
  <section class="workspaces" :aria-busy="loading || saving">
    <h1>{{ i18n.t('workspaces') }}</h1>
    <p class="description">{{ i18n.t('workspacesDescription') }}</p>
    <WorkspaceCreateForm :key="formVersion" :disabled="saving" @create="create" />
    <p v-if="message" role="status">{{ message }}</p>
    <div v-if="error" class="workspace-feedback">
      <p role="alert">{{ error }}</p>
      <Button :label="i18n.t('workspaceRefresh')" :disabled="loading || saving" @click="load()" />
    </div>
    <p v-if="loading" role="status">{{ i18n.t('workspacesLoading') }}</p>
    <template v-else-if="!error">
      <p v-if="items.length === 0">{{ i18n.t('workspacesEmpty') }}</p>
      <ul v-else class="workspace-list">
        <li v-for="workspace in items" :key="workspace.id">
          <div class="workspace-summary">
            <h2>{{ workspace.name }}</h2>
            <p>{{ i18n.t('workspaceRepositoryCount') }}: {{ workspace.repositoryCount }}</p>
          </div>
          <div class="workspace-row-actions" role="group" :aria-label="i18n.t('workspaceActions') + ': ' + workspace.name">
            <RouterLink class="workspace-dashboard-link" :to="'/workspaces/' + workspace.id">{{ i18n.t('dashboardOpen') }}</RouterLink>
            <Button :label="i18n.t('renameWorkspace')" severity="secondary" :disabled="saving"
              @click="openAction(workspace, 'rename')" />
            <Button :label="i18n.t('workspaceRepositories')" severity="secondary" :disabled="saving"
              @click="repositoryWorkspace = workspace" />
            <button type="button" class="workspace-danger-action" :disabled="saving"
              @click="openAction(workspace, 'delete')">{{ i18n.t('deleteWorkspace') }}</button>
          </div>
        </li>
      </ul>
      <nav v-if="totalPages > 1" class="workspace-pagination" :aria-label="i18n.t('workspacePagination')">
        <Button :label="i18n.t('previousPage')" :disabled="page === 0 || saving" @click="load(page - 1)" />
        <span>{{ page + 1 }} / {{ totalPages }}</span>
        <Button :label="i18n.t('nextPage')" :disabled="page + 1 >= totalPages || saving" @click="load(page + 1)" />
      </nav>
    </template>
    <WorkspaceActionDialog v-if="action" :key="action.mode + ':' + action.workspace.id"
      :workspace="action.workspace" :mode="action.mode" :busy="saving" :error="dialogError"
      @rename="rename(action.workspace.id, $event)" @delete="remove(action.workspace.id)" @cancel="cancelAction" />
    <WorkspaceRepositoriesDialog v-if="repositoryWorkspace" :key="repositoryWorkspace.id" :workspace="repositoryWorkspace"
      @changed="load()" @close="repositoryWorkspace = null" />
  </section>
</template>

<style scoped>
.workspace-list { list-style: none; padding: 0; display: grid; gap: 1rem; }
.workspace-list li { padding: 1.5rem; background: var(--gc-surface); border: 1px solid var(--gc-divider); border-radius: .75rem; overflow-wrap: anywhere; display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1.5rem; }
.workspace-summary { min-width: 0; }
.workspace-list p { color: var(--gc-text-muted); margin-bottom: 0; }
.workspace-row-actions { display: flex; flex-wrap: wrap; gap: .75rem; }
.workspace-dashboard-link { align-self: center; color: var(--gc-primary); }
.workspace-pagination { display: flex; align-items: center; justify-content: center; gap: 1rem; }
.workspace-feedback { margin-block: 1rem; }
</style>
