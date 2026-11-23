<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import Button from 'primevue/button';
import { WorkspaceDashboardApiService } from '../../../core/workspaces/workspace-dashboard-api.service';
import type { WorkspaceDashboardResponse } from '../../../core/workspaces/models/responses/workspace-dashboard-response.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import RepositoryOverview from '../../explorer/repository-overview/RepositoryOverview.vue';
import WorkspaceRepositoriesDialog from '../workspace-repositories-dialog/WorkspaceRepositoriesDialog.vue';

const props = defineProps<{ workspaceId: number }>();
const emit = defineEmits<{ catalogChanged: [] }>();
const i18n = useLanguageStore();
const api = new WorkspaceDashboardApiService(() => i18n.language, i18n.t);
const dashboard = ref<WorkspaceDashboardResponse | null>(null);
const loading = ref(true);
const error = ref('');
const managing = ref(false);
const repositories = computed(() => dashboard.value?.repositories.map(repo => ({ ...repo, workspaceIds: [props.workspaceId] })) ?? []);
let request: AbortController | undefined;
async function load() {
  request?.abort();
  const current = new AbortController();
  request = current;
  loading.value = true; dashboard.value = null; error.value = '';
  try {
    const response = await api.getDashboard(props.workspaceId, current.signal);
    if (!current.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('dashboardError'));
      dashboard.value = response.result;
    }
  } catch (cause: unknown) {
    if (!current.signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('dashboardError');
  } finally { if (!current.signal.aborted) loading.value = false; }
}
onMounted(load);
watch([() => props.workspaceId, () => i18n.language], load);
onUnmounted(() => request?.abort());
</script>
<template>
  <section class="workspace-dashboard" :aria-busy="loading">
    <RouterLink class="overview-back" to="/">{{ i18n.t('missionControl') }}</RouterLink>
    <div class="page-heading">
<div>
<h1>{{ dashboard ? dashboard.name : i18n.t('dashboardTitle') }}</h1>
<p class="page-description">{{ i18n.t('workspaceOverview') }}</p>
</div>
<div class="page-actions">
<Button :label="i18n.t('dashboardRefresh')" severity="secondary" :disabled="loading" @click="load" />
<Button :label="i18n.t('workspaceRepositories')" :disabled="!dashboard || loading" @click="managing = true" />
</div>
</div>
    <p v-if="error" role="alert">{{ error }}</p>
    <RepositoryOverview v-else :repositories="repositories" :workspace-id="workspaceId" :scope-key="'workspace:' + workspaceId" :loading="loading" />
    <p v-if="dashboard && !loading && !error && repositories.length === 0" class="muted">{{ i18n.t('dashboardEmpty') }}</p>
    <WorkspaceRepositoriesDialog v-if="managing && dashboard" :workspace="{ id: workspaceId, name: dashboard.name }" @close="managing = false" @changed="load(); emit('catalogChanged')" />
  </section>
</template>