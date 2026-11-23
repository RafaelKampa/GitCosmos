<script setup lang="ts">
import { computed, onUnmounted, watch } from 'vue';
import { RouterLink, RouterView, useRoute } from 'vue-router';
import AppHeader from '../app-header/AppHeader.vue';
import ExplorerSidebar from '../explorer-sidebar/ExplorerSidebar.vue';
import OperationsDrawer from '../operations-drawer/OperationsDrawer.vue';
import { useExplorerStore } from '../../core/catalog/explorer.store';
import { useExplorerPreferences } from '../../core/preferences/explorer-preferences.store';
import { useLanguageStore } from '../../core/i18n/language.store';
const route = useRoute(); const explorer = useExplorerStore(); const preferences = useExplorerPreferences(); const i18n = useLanguageStore();
const context = computed(() => {
  const repositoryId = Number(route.params.repositoryId);
  if (repositoryId) return explorer.index.repositories.find(repo => repo.id === repositoryId)?.name ?? i18n.t('changesTitle');
  const workspaceId = Number(route.params.id);
  if (workspaceId) return explorer.index.workspaces.find(workspace => workspace.id === workspaceId)?.name ?? i18n.t('workspaces');
  if (route.path === '/repositories') return i18n.t('allRepositories');
  if (route.path === '/settings') return i18n.t('settings');
  if (route.path === '/workspaces') return i18n.t('manageWorkspaces');
  if (route.path === '/connection') return i18n.t('statusTitle');
  return i18n.t('missionControl');
});
watch([() => route.fullPath, () => i18n.language], () => { void explorer.refresh(); }, { immediate: true });
onUnmounted(explorer.cancel);
</script>
<template>
  <div class="app-shell" :class="{ 'sidebar-collapsed': preferences.collapsed }" :style="{ '--explorer-width': preferences.sidebarWidth + 'px' }">
    <a class="skip-link" href="#app-content">{{ i18n.t('skipToContent') }}</a>
    <AppHeader />
<ExplorerSidebar :active-repository-id="Number(route.params.repositoryId)" />
    <main id="app-content" class="app-content" tabindex="-1">
      <nav class="breadcrumbs" :aria-label="i18n.t('breadcrumbs')">
<RouterLink to="/">GitCosmos</RouterLink>
<span aria-hidden="true">/</span>
<span aria-current="page">{{ context }}</span>
</nav>
      <RouterView v-slot="{ Component }">
<component :is="Component" @catalog-changed="explorer.refresh" />
</RouterView>
    </main>
    <OperationsDrawer />
    <footer class="app-status-bar">
<span :class="{ offline: explorer.error }">
<span class="connection-dot" aria-hidden="true">
</span>{{ i18n.t(explorer.loading ? 'statusChecking' : explorer.error ? 'offline' : 'ready') }}</span>
<span>{{ explorer.index.repositories.length }} {{ i18n.t('repositoriesMetric') }} · {{ i18n.t('localMode') }}</span>
</footer>
  </div>
</template>
