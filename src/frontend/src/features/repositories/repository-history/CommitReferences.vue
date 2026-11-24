<script setup lang="ts">
import { computed } from 'vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { parseCommitReferences } from './history-presentation';
const props = defineProps<{ value: string }>();
const i18n = useLanguageStore();
const references = computed(() => parseCommitReferences(props.value));
const labels = { branch: 'historyBranchRef', remote: 'historyRemoteRef', tag: 'historyTagRef', head: 'historyHeadRef' } as const;
</script>

<template>
  <span class="commit-refs">
    <span v-for="reference in references" :key="reference.fullName" class="reference-pill" :class="[reference.kind, { current: reference.current }]" :title="i18n.t(labels[reference.kind]) + ': ' + reference.fullName">
      <svg v-if="reference.kind === 'remote'" viewBox="0 0 16 16" aria-hidden="true"><path d="M2 2h12v9H2zM5 14h6M8 11v3" /></svg>
      <svg v-else-if="reference.kind === 'tag'" viewBox="0 0 16 16" aria-hidden="true"><path d="M2 2h6l6 6-6 6-6-6z" /><circle cx="5" cy="5" r="1" /></svg>
      <svg v-else viewBox="0 0 16 16" aria-hidden="true"><circle cx="4" cy="3" r="2" /><circle cx="12" cy="3" r="2" /><circle cx="4" cy="13" r="2" /><path d="M4 5v6m0-3h4q4 0 4-3" /></svg>
      <span v-if="reference.current" class="current-mark" aria-hidden="true">✓</span>{{ reference.label }}
    </span>
  </span>
</template>

<style scoped>
.commit-refs { display: inline-flex; align-items: center; gap: .3rem; min-width: 0; }
.reference-pill { display: inline-flex; align-items: center; gap: .2rem; flex-shrink: 0; border: 1px solid var(--gc-violet); border-radius: .25rem; padding: .05rem .3rem; font-size: .72rem; line-height: 1.2; color: var(--gc-text); background: var(--gc-selection-bg); white-space: nowrap; }
.remote { border-color: var(--gc-info); background: var(--gc-info-bg); }
.tag { border-color: var(--gc-warning); background: var(--gc-warning-bg); }
.current { font-weight: 700; border-color: var(--gc-primary); }
svg { width: 12px; height: 12px; fill: none; stroke: currentColor; stroke-width: 1.2; }
.current-mark { color: var(--gc-primary); }
</style>
