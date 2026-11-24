<script setup lang="ts">
import MaterialIcon from '../../shared/components/material-icon/MaterialIcon.vue';
import { computed, ref, useId } from 'vue';
import { RouterLink } from 'vue-router';
import { useLanguageStore } from '../../core/i18n/language.store';
import { useExplorerPreferences } from '../../core/preferences/explorer-preferences.store';
import RepositoryBranches from '../../features/repositories/repository-branches/RepositoryBranches.vue';
const props = withDefaults(defineProps<{ repositoryId: number; workspaceId?: number; name: string; to: string; active: boolean }>(), { workspaceId: 0 });
const resourceOpen = ref<Record<string, boolean>>({});
const i18n = useLanguageStore(); const preferences = useExplorerPreferences(); const id = useId();
const expanded = computed(() => preferences.expandedRepositories[props.to] === true);
const sections = [
  { key: 'branches', label: 'repositoryBranchesTab', icon: 'account_tree' },
  { key: 'remotes', label: 'repositoryRemotesTab', icon: 'cloud' },
  { key: 'tags', label: 'repositoryTags', icon: 'label' },
  { key: 'stashes', label: 'repositoryStashes', icon: 'inventory_2' },
  { key: 'submodules', label: 'repositorySubmodules', icon: 'extension' },
] as const;
</script>

<template>
  <li class="explorer-repository">
    <div class="repository-tree-heading" :class="{ 'explorer-active': active }">
      <button type="button" class="tree-toggle repository-toggle" :aria-expanded="expanded" :aria-controls="id" :aria-label="i18n.t('toggleRepository') + ': ' + name" @click="preferences.expandedRepositories[to] = !expanded; resourceOpen = {}"><MaterialIcon :name="expanded ? 'expand_more' : 'chevron_right'" /></button>
      <RouterLink class="repository-tree-link" :to="to" :title="name" :aria-current="active ? 'page' : undefined">{{ name }}</RouterLink>
    </div>
    <ul v-if="expanded" :id="id" class="repository-tree-sections">
      <li><RouterLink class="repository-section-link" :to="to" @click="preferences.repositoryTabs[repositoryId] = 'changes'">{{ i18n.t('repositoryChangesTab') }}</RouterLink></li>
      <li><RouterLink class="repository-section-link" :to="to" @click="preferences.repositoryTabs[repositoryId] = 'history'">{{ i18n.t('repositoryHistoryTab') }}</RouterLink></li>
      <li v-for="section in sections" :key="section.key">
        <details class="repository-resource-group" @toggle="resourceOpen[section.key] = ($event.target as HTMLDetailsElement).open">
          <summary><MaterialIcon class="resource-icon" :name="section.icon" />{{ i18n.t(section.label) }}</summary>
          <template v-if="section.key === 'branches' || section.key === 'remotes'"><RepositoryBranches v-if="resourceOpen[section.key]" :repository-id="repositoryId" :workspace-id="workspaceId" :remote-mode="section.key === 'remotes'" /></template>
          <p v-else class="repository-resource-notice">{{ i18n.t('repositoryResourcesUpcoming') }}</p>
        </details>
      </li>
    </ul>
  </li>
</template>

<style scoped>
.repository-tree-heading { display: flex; align-items: center; min-width: 0; border-radius: .3rem; }
.repository-toggle { flex: 0 0 1.6rem; min-height: 1.75rem; cursor: pointer; }
.repository-tree-link { display: block; flex: 1; min-width: 0; padding: .35rem .25rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--gc-text); font-size: .85rem; }
.explorer-repository > .repository-tree-sections { list-style: none; margin: 0 0 .4rem .7rem; padding: .2rem 0 .2rem var(--gc-tree-indent); border-left: 1px solid var(--gc-divider); }
.repository-section-link { display: block; padding: .3rem .4rem; font-size: .8rem; color: var(--gc-text-muted); }
summary { padding: .35rem .2rem; cursor: pointer; color: var(--gc-text); font-size: .8rem; }
summary:focus-visible { outline: 2px solid var(--gc-violet); outline-offset: -2px; }
.resource-icon { display: inline-block; width: 1.2rem; color: var(--gc-text-muted); }
.repository-resource-notice { margin: .2rem .4rem .5rem; padding-left: .5rem; color: var(--gc-text-muted); font-size: .75rem; line-height: 1.4; }
</style>
