<script setup lang="ts">
import { computed } from 'vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
const props = defineProps<{ status: string }>();
const i18n = useLanguageStore();
const states = {
  A: { symbol: '+', tone: 'added', label: 'gitAddedFiles' },
  D: { symbol: '−', tone: 'deleted', label: 'gitDeletedFiles' },
  M: { symbol: 'M', tone: 'modified', label: 'gitModifiedFiles' },
  R: { symbol: 'R', tone: 'modified', label: 'gitRenamedFiles' },
  U: { symbol: 'U', tone: 'deleted', label: 'gitConflictedFiles' },
  '?': { symbol: '+', tone: 'added', label: 'gitUntrackedFiles' },
} as const;
const state = computed(() => states[props.status[0] as keyof typeof states] ?? { symbol: props.status || '?', tone: 'modified', label: 'gitChangedFiles' as const });
</script>

<template><span class="file-status" :class="state.tone" :title="i18n.t(state.label) + ' (' + status + ')'" :aria-label="i18n.t(state.label)">{{ state.symbol }}</span></template>

<style scoped>
.file-status { display: inline-flex; align-items: center; justify-content: center; min-width: 1.25rem; flex-shrink: 0; padding: .1rem .25rem; border-radius: .2rem; font: 600 .75rem monospace; line-height: 1.2; }
.modified { color: var(--gc-warning); background: var(--gc-warning-bg); }
.added { color: var(--gc-success); background: var(--gc-success-bg); }
.deleted { color: var(--gc-danger); background: var(--gc-danger-bg); }
</style>
