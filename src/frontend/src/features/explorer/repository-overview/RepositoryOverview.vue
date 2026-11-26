<script setup lang="ts">
import MaterialIcon from '../../../shared/components/material-icon/MaterialIcon.vue';
import { computed } from 'vue';
import { RouterLink } from 'vue-router';
import type { CatalogRepository } from '../../../core/catalog/catalog.model';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';
import { useLanguageStore } from '../../../core/i18n/language.store';
import SummaryMetrics from '../../../shared/components/summary-metrics/SummaryMetrics.vue';

const props = defineProps<{ repositories: readonly CatalogRepository[]; scopeKey: string; workspaceId?: number; loading: boolean }>();
const i18n = useLanguageStore(); const preferences = useExplorerPreferences();
const filters = computed(() => preferences.scope(props.scopeKey));
const visible = computed(() => props.repositories.filter(repo =>
  (repo.name + ' ' + repo.localPath + ' ' + (repo.status?.branch ?? '')).toLocaleLowerCase().includes(filters.value.search.toLocaleLowerCase())
  && (filters.value.status === 'all' || repo.state === filters.value.status)
  && (!filters.value.favoritesOnly || preferences.favorites.includes(repo.id)))
  .sort((a, b) => a.name.localeCompare(b.name, i18n.language) * (filters.value.sort === 'descending' ? -1 : 1)));
const selectedIds = computed(() => preferences.selected[props.scopeKey] ?? []);
const selectedCount = computed(() => props.repositories.filter(repo => selectedIds.value.includes(repo.id)).length);
const allVisible = computed(() => visible.value.length > 0 && visible.value.every(repo => selectedIds.value.includes(repo.id)));
const labels = { clean: 'gitClean', modified: 'gitModified', conflicted: 'gitConflicted', unavailable: 'gitUnavailable', unknown: 'statusUnknown' } as const;
function clear() { Object.assign(filters.value, { search: '', status: 'all', favoritesOnly: false }); }
function repositoryUrl(id: number) { return props.workspaceId ? '/workspaces/' + props.workspaceId + '/repositories/' + id : '/repositories/' + id; }
</script>
<template>
  <SummaryMetrics :repositories="repositories" :loading="loading" />
  <slot name="between" />
  <section class="repository-overview" :aria-label="i18n.t('repositoriesMetric')">
    <div class="section-heading"><h2>{{ i18n.t('repositoriesMetric') }}</h2><span class="muted">{{ visible.length }} / {{ repositories.length }}</span></div>
    <div class="repository-filters">
      <input v-model="filters.search" type="search" :aria-label="i18n.t('searchRepositories')" :placeholder="i18n.t('searchRepositories')" />
      <select v-model="filters.status" :aria-label="i18n.t('filterStatus')">
        <option value="all">{{ i18n.t('allStates') }}</option>
        <option v-for="(label, state) in labels" :key="state" :value="state">{{ i18n.t(label) }}</option>
      </select>
      <select v-model="filters.sort" :aria-label="i18n.t('sortRepositories')"><option value="name">A → Z</option><option value="descending">Z → A</option></select>
      <button type="button" class="icon-action" :aria-pressed="filters.favoritesOnly" :aria-label="i18n.t('favoritesOnly')" @click="filters.favoritesOnly = !filters.favoritesOnly"><MaterialIcon name="star_border" /></button>
      <div class="view-toggle" role="group" :aria-label="i18n.t('repositoryView')">
        <button type="button" :aria-pressed="preferences.view === 'list'" @click="preferences.view = 'list'">☷ {{ i18n.t('listView') }}</button>
        <button type="button" :aria-pressed="preferences.view === 'cards'" @click="preferences.view = 'cards'">▦ {{ i18n.t('cardsView') }}</button>
      </div>
    </div>
    <div class="bulk-toolbar">
      <label><input type="checkbox" :checked="allVisible" :disabled="loading || visible.length === 0" @change="preferences.selectVisible(scopeKey, visible.map(repo => repo.id), !allVisible)" />{{ i18n.t('selectVisible') }}</label>
      <span>{{ selectedCount }} {{ i18n.t('selectedRepositories') }}</span>
      <div class="bulk-actions" :title="i18n.t('operationsUpcoming')"><button disabled>Fetch {{ i18n.t('selectionScope') }}</button><button disabled>Pull {{ i18n.t('selectionScope') }}</button><button disabled>Push {{ i18n.t('selectionScope') }}</button></div>
    </div>
    <p v-if="loading" role="status">{{ i18n.t('dashboardLoading') }}</p>
    <div v-else-if="visible.length === 0" class="empty-panel"><p>{{ i18n.t('noMatchingRepositories') }}</p><button type="button" class="text-action" @click="clear">{{ i18n.t('clearFilters') }}</button></div>
    <ul v-else class="repository-collection" :class="preferences.view">
      <li v-for="repo in visible" :key="repo.id" class="repository-entry">
        <input type="checkbox" :aria-label="i18n.t('selectRepository') + ': ' + repo.name" :checked="selectedIds.includes(repo.id)" @change="preferences.toggleSelection(scopeKey, repo.id)" />
        <RouterLink class="repository-open-link" :to="repositoryUrl(repo.id)"><strong :class="{ 'has-local-changes': !!repo.status && repo.status.changedFiles > 0 }"><span v-if="repo.status && repo.status.changedFiles > 0" class="local-changes-dot" aria-hidden="true" />{{ repo.name }}</strong><small>{{ repo.localPath }}</small></RouterLink>
        <span class="repository-branch">{{ repo.status ? (repo.status.isDetached ? i18n.t('gitDetached') : repo.status.branch) : '—' }}</span>
        <span class="repository-state" :class="repo.state" :title="repo.error ?? undefined">{{ i18n.t(labels[repo.state]) }}</span>
        <span class="repository-divergence" :title="i18n.t('localTracking')">↓ {{ repo.status?.behind ?? '—' }} · ↑ {{ repo.status?.ahead ?? '—' }}</span>
        <button type="button" class="favorite-action" :aria-label="i18n.t('favoriteRepository') + ': ' + repo.name" :aria-pressed="preferences.favorites.includes(repo.id)" @click="preferences.toggleFavorite(repo.id)"><MaterialIcon :name="preferences.favorites.includes(repo.id) ? 'star' : 'star_border'" /></button>
        <p v-if="repo.error" class="repository-row-error" role="alert">{{ repo.error }}</p>
      </li>
    </ul>
    <small class="muted">{{ i18n.t('localTracking') }} · {{ i18n.t('operationsUpcoming') }}</small>
  </section>
</template>
