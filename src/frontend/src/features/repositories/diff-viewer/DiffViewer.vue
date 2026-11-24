<script setup lang="ts">
import { computed } from 'vue';
import type { FileDiff } from '../../../core/repositories/models/repository-changes.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { parseUnifiedDiff } from './unified-diff';

const props = defineProps<{ diff: FileDiff }>();
const i18n = useLanguageStore();
const lines = computed(() => parseUnifiedDiff(props.diff.patch).filter(line => line.type === 'addition' || line.type === 'deletion' || line.type === 'context'));
const messages = { binary: 'diffBinary', tooLarge: 'diffTooLarge', submodule: 'diffSubmodule', conflict: 'diffConflict', empty: 'diffEmpty' } as const;
</script>

<template>
  <div v-if="diff.kind === 'text' && lines.length" class="diff-viewer" tabindex="0" role="region" :aria-label="i18n.t('changesDiff')">
    <div v-for="(line, index) in lines" :key="index" class="diff-line" :class="line.type">
      <span class="line-number" aria-hidden="true">{{ line.oldLine }}</span>
      <span class="line-number" aria-hidden="true">{{ line.newLine }}</span>
      <code>{{ line.text }}</code>
    </div>
  </div>
  <p v-else class="diff-notice" role="status">{{ i18n.t(diff.kind === 'text' ? 'diffEmpty' : messages[diff.kind]) }}</p>
</template>

<style scoped>
.diff-viewer { overflow: auto; max-height: 70vh; background: var(--gc-bg); font-size: .85rem; line-height: 1.6; }
.diff-viewer:focus-visible { outline: 2px solid var(--gc-violet); outline-offset: -2px; }
.diff-line { display: flex; min-width: max-content; white-space: pre; font-family: monospace; }
.diff-line code { padding-inline: .75rem; }
.line-number { width: 3.5rem; flex-shrink: 0; text-align: right; padding-inline: .5rem; color: var(--gc-text-muted); user-select: none; border-right: 1px solid var(--gc-divider); }
.addition { background: var(--gc-success-bg); color: var(--gc-success); }
.deletion { background: var(--gc-danger-bg); color: var(--gc-danger); }
.diff-notice { padding: 1rem; color: var(--gc-text-muted); }
</style>
