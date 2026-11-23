<script setup lang="ts">
import { computed, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { useExplorerStore } from '../../core/catalog/explorer.store';
import { useLanguageStore } from '../../core/i18n/language.store';
import { useExplorerPreferences } from '../../core/preferences/explorer-preferences.store';
const i18n = useLanguageStore(); const explorer = useExplorerStore(); const preferences = useExplorerPreferences();
const closed = ref<number[]>([]);
defineProps<{ activeRepositoryId?: number }>();
const ungrouped = computed(() => explorer.index.repositories.filter(repo => repo.workspaceIds.length === 0));
function toggle(id: number) { closed.value = closed.value.includes(id) ? closed.value.filter(value => value !== id) : [...closed.value, id]; }
</script>
<template>
<aside id="explorer-sidebar" class="explorer-sidebar" :inert="preferences.collapsed ? true : undefined" :aria-label="i18n.t('explorer')">
<h2 class="explorer-heading">{{ i18n.t('explorer') }}</h2>
<nav :aria-label="i18n.t('navigation')">
  <RouterLink to="/" class="explorer-root">
<span aria-hidden="true">▦</span>{{ i18n.t('missionControl') }}</RouterLink>
<RouterLink to="/repositories" class="explorer-root">
<span aria-hidden="true">▤</span>{{ i18n.t('allRepositories') }}</RouterLink>
  <div class="explorer-section-label">
<span>{{ i18n.t('workspaces') }}</span>
<RouterLink to="/workspaces" :aria-label="i18n.t('manageWorkspaces')">＋</RouterLink>
</div>
  <p v-if="explorer.loading" class="explorer-feedback">{{ i18n.t('explorerLoading') }}</p>
<div v-if="explorer.error" class="explorer-feedback">
<p role="alert">{{ explorer.error }}</p>
<button class="text-action" @click="explorer.refresh">{{ i18n.t('diffRetry') }}</button>
</div>
  <div v-for="workspace in explorer.index.workspaces" :key="workspace.id" class="explorer-workspace">
<div class="explorer-workspace-heading">
<button class="tree-toggle" type="button" :aria-label="i18n.t('toggleWorkspace') + ': ' + workspace.name" :aria-expanded="!closed.includes(workspace.id)" @click="toggle(workspace.id)">{{ closed.includes(workspace.id) ? '▸' : '▾' }}</button>
<RouterLink :to="'/workspaces/' + workspace.id">{{ workspace.name }}</RouterLink>
</div>
<ul v-if="!closed.includes(workspace.id)">
<li v-for="repo in explorer.index.repositories.filter(item => item.workspaceIds.includes(workspace.id))" :key="repo.id">
<RouterLink :class="{ 'explorer-active': activeRepositoryId === repo.id }" :to="'/workspaces/' + workspace.id + '/repositories/' + repo.id">{{ repo.name }}</RouterLink>
</li>
</ul>
</div>
  <p v-if="!explorer.loading && !explorer.error && explorer.index.workspaces.length === 0" class="explorer-feedback">{{ i18n.t('explorerNoWorkspaces') }}</p>
<div class="explorer-section-label">{{ i18n.t('ungrouped') }}</div>
<ul class="explorer-ungrouped">
<li v-for="repo in ungrouped" :key="repo.id">
<RouterLink :to="'/repositories/' + repo.id">
<span aria-hidden="true">▤</span>{{ repo.name }}</RouterLink>
</li>
</ul>
<p v-if="!explorer.loading && !explorer.error && ungrouped.length === 0" class="explorer-feedback">{{ i18n.t('ungroupedEmpty') }}</p>
  <button type="button" class="explorer-root operations-toggle" :aria-expanded="preferences.operationsOpen" aria-controls="operations-drawer" @click="preferences.operationsOpen = !preferences.operationsOpen">
<span aria-hidden="true">◷</span>{{ i18n.t('operations') }}</button>
</nav>
</aside>
</template>
