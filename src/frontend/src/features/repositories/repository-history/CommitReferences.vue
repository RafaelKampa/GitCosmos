<script setup lang="ts">
import MaterialIcon from '../../../shared/components/material-icon/MaterialIcon.vue';
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
      <MaterialIcon :name="reference.kind === 'remote' ? 'cloud' : reference.kind === 'tag' ? 'label' : 'account_tree'" />
      <MaterialIcon v-if="reference.current" class="current-mark" name="check" />{{ reference.label }}
    </span>
  </span>
</template>

<style scoped>
.commit-refs { display: inline-flex; align-items: center; gap: .3rem; min-width: 0; }
.reference-pill { display: inline-flex; align-items: center; gap: .2rem; flex-shrink: 0; border: 1px solid var(--gc-violet); border-radius: .25rem; padding: .05rem .3rem; font-size: .72rem; line-height: 1.2; color: var(--gc-text); background: var(--gc-selection-bg); white-space: nowrap; }
.remote { border-color: var(--gc-info); background: var(--gc-info-bg); }
.tag { border-color: var(--gc-warning); background: var(--gc-warning-bg); }
.current { font-weight: 700; border-color: var(--gc-primary); }
.current-mark { color: var(--gc-primary); }
</style>
