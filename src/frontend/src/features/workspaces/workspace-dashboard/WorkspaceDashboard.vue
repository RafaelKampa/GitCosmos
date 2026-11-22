<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import Button from 'primevue/button';
import { WorkspaceDashboardApiService } from '../../../core/workspaces/workspace-dashboard-api.service';
import type { WorkspaceDashboardResponse } from '../../../core/workspaces/models/responses/workspace-dashboard-response.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import RepositoryStatusCard from '../repository-status-card/RepositoryStatusCard.vue';

const props = defineProps<{ workspaceId: number }>();
const i18n = useLanguageStore();
const api = new WorkspaceDashboardApiService(() => i18n.language, i18n.t);
const dashboard = ref<WorkspaceDashboardResponse | null>(null);
const loading = ref(true);
const error = ref('');
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
    <RouterLink to="/workspaces">{{ i18n.t('dashboardBack') }}</RouterLink>
    <div class="dashboard-heading">
      <h1>{{ i18n.t('dashboardTitle') }}<template v-if="dashboard"> — {{ dashboard.name }}</template></h1>
      <Button :label="i18n.t('dashboardRefresh')" :disabled="loading" @click="load" />
    </div>
    <p>{{ i18n.t('dashboardDescription') }}</p>
    <p v-if="loading" role="status">{{ i18n.t('dashboardLoading') }}</p>
    <p v-else-if="error" role="alert">{{ error }}</p>
    <template v-else-if="dashboard">
      <p>{{ i18n.t('workspaceRepositoryCount') }}: {{ dashboard.repositories.length }}</p>
      <p v-if="dashboard.repositories.length === 0">{{ i18n.t('dashboardEmpty') }}</p>
      <div v-else class="dashboard-repositories">
        <div v-for="repository in dashboard.repositories" :key="repository.id" class="dashboard-repository-item">
          <RepositoryStatusCard :repository="repository" />
          <RouterLink class="repository-open-link" :to="'/workspaces/' + workspaceId + '/repositories/' + repository.id">{{ i18n.t('changesOpen') }} — {{ repository.name }}</RouterLink>
        </div>
      </div>
      <p class="dashboard-counts-help">{{ i18n.t('dashboardCountsHelp') }}</p>
    </template>
  </section>
</template>

<style scoped>
.dashboard-heading { display: flex; align-items: center; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
.dashboard-heading h1 { overflow-wrap: anywhere; }
.dashboard-repositories { display: grid; gap: 1.5rem; }
.repository-open-link { display: inline-block; margin-top: .75rem; color: var(--gc-primary); }
.dashboard-counts-help { color: var(--gc-text-muted); }
</style>
