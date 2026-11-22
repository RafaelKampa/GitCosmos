<script setup lang="ts">
import type { RepositoryDashboardResponse } from '../../../core/workspaces/models/responses/workspace-dashboard-response.model';
import { useLanguageStore } from '../../../core/i18n/language.store';

defineProps<{ repository: RepositoryDashboardResponse }>();
const i18n = useLanguageStore();
const labels = { clean: 'gitClean', modified: 'gitModified', conflicted: 'gitConflicted', unavailable: 'gitUnavailable' } as const;
const counters = { changedFiles: 'gitChangedFiles', modified: 'gitModifiedFiles', added: 'gitAddedFiles', deleted: 'gitDeletedFiles',
  renamed: 'gitRenamedFiles', untracked: 'gitUntrackedFiles', conflicted: 'gitConflictedFiles', staged: 'gitStagedFiles', unstaged: 'gitUnstagedFiles' } as const;
</script>

<template>
  <article class="repository-status-card" :class="repository.state">
    <div class="repository-status-header">
      <h2>{{ repository.name }}</h2>
      <span class="git-state">{{ i18n.t(labels[repository.state]) }}</span>
    </div>
    <code class="repository-location">{{ repository.localPath }}</code>
    <template v-if="repository.status">
      <p class="git-branch">{{ i18n.t('gitBranch') }}:
        <strong>{{ repository.status.isDetached ? i18n.t('gitDetached') : repository.status.branch }}</strong>
      </p>
      <p v-if="!repository.status.hasCommits">{{ i18n.t('gitNoCommits') }}</p>
      <dl class="git-counts">
        <div v-for="(label, key) in counters" :key="key">
          <dt>{{ i18n.t(label) }}</dt><dd>{{ repository.status[key] }}</dd>
        </div>
      </dl>
    </template>
    <p v-else role="alert">{{ repository.error || i18n.t('dashboardRepositoryError') }}</p>
  </article>
</template>

<style scoped>
.repository-status-card { padding: 1.5rem; border: 1px solid var(--gc-border); border-radius: .75rem; background: var(--gc-surface); }
.repository-status-header { display: flex; align-items: center; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
h2 { margin: 0; overflow-wrap: anywhere; }
.git-state { padding: .35rem .65rem; border-radius: .5rem; color: var(--gc-success); background: var(--gc-success-bg); }
.modified .git-state { color: var(--gc-warning); background: var(--gc-warning-bg); }
.conflicted .git-state, .unavailable .git-state { color: var(--gc-danger); background: var(--gc-danger-bg); }
.repository-location { display: block; overflow-wrap: anywhere; margin-block: 1rem; color: var(--gc-text-muted); }
.git-branch { overflow-wrap: anywhere; }
.git-counts { display: grid; grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr)); gap: 1rem; margin-bottom: 0; }
dt { color: var(--gc-text-muted); font-size: .9rem; }
dd { margin: .35rem 0 0; font-weight: 600; font-size: 1.25rem; }
</style>
