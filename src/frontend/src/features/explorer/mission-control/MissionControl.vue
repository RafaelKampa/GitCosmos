<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import Button from 'primevue/button';
import { CatalogApiService } from '../../../core/catalog/catalog-api.service';
import type { CatalogSnapshot } from '../../../core/catalog/catalog.model';
import { useExplorerStore } from '../../../core/catalog/explorer.store';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import RepositoryOverview from '../repository-overview/RepositoryOverview.vue';
import CatalogAddDialog from '../catalog-add-dialog/CatalogAddDialog.vue';
const props = defineProps<{ all?: boolean }>();
const i18n = useLanguageStore(); const explorer = useExplorerStore(); const api = new CatalogApiService(() => i18n.language, i18n.t);
const data = ref<CatalogSnapshot>({ workspaces: [], repositories: [] }); const loading = ref(true); const error = ref(''); const adding = ref(false);
let request: AbortController | undefined;
async function load() {
  request?.abort(); const current = new AbortController(); request = current; loading.value = true; error.value = '';
  try { const response = await api.getOverview(current.signal); if (!current.signal.aborted) { if (!response.result || !Array.isArray(response.result.repositories) || !Array.isArray(response.result.workspaces)) throw new ApiError(i18n.t('explorerError')); data.value = response.result; explorer.index = response.result; } }
  catch (cause: unknown) { if (!current.signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('explorerError'); }
  finally { if (!current.signal.aborted) loading.value = false; }
}
onMounted(load); watch(() => i18n.language, load); onUnmounted(() => request?.abort());
</script>
<template>
<section class="mission-control" :aria-busy="loading">
  <div class="page-heading">
<div>
<h1>{{ i18n.t(props.all ? 'allRepositories' : 'missionControl') }}</h1>
<p class="page-description">{{ i18n.t('missionDescription') }}</p>
</div>
<div class="page-actions">
<Button severity="secondary" :label="i18n.t('dashboardRefresh')" :disabled="loading" @click="load" />
<Button :label="i18n.t('addRepository')" @click="adding = true" />
</div>
</div>
  <p v-if="error" role="alert">{{ error }}</p>
<RepositoryOverview v-else :repositories="data.repositories" :scope-key="props.all ? 'all' : 'mission'" :loading="loading">
<template #between>  <div v-if="!props.all" class="mission-workspaces">
<div class="section-heading">
<h2>{{ i18n.t('workspaces') }}</h2>
<RouterLink to="/workspaces">{{ i18n.t('manageWorkspaces') }}</RouterLink>
</div>
<div class="workspace-tiles">
<RouterLink v-for="workspace in data.workspaces" :key="workspace.id" class="workspace-tile" :to="'/workspaces/' + workspace.id">
<strong>▤ &nbsp; {{ workspace.name }}</strong>
<small>{{ data.repositories.filter(repo => repo.workspaceIds.includes(workspace.id)).length }} {{ i18n.t('repositoriesMetric') }}</small>
</RouterLink>
<p v-if="!loading && data.workspaces.length === 0" class="muted">{{ i18n.t('explorerNoWorkspaces') }}</p>
</div>
</div>
</template>
</RepositoryOverview>
<CatalogAddDialog v-if="adding" @close="adding = false" @changed="load" />
</section>
</template>
