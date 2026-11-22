<script setup lang="ts">
import { computed } from 'vue';
import type { GitFileChange, DiffSelection } from '../../../core/repositories/models/repository-changes.model';
import { useLanguageStore } from '../../../core/i18n/language.store';

const props = defineProps<{ files: readonly GitFileChange[]; selected: DiffSelection | null }>();
defineEmits<{ select: [selection: DiffSelection] }>();
const i18n = useLanguageStore();
const groups = computed(() => [
  { area: 'staged' as const, label: 'changesStaged' as const, files: props.files.filter(file => file.indexStatus !== '.' && !file.isConflicted) },
  { area: 'working' as const, label: 'changesWorking' as const, files: props.files.filter(file => file.workTreeStatus !== '.') },
]);
</script>

<template>
  <aside class="file-changes-list" :aria-label="i18n.t('changesFiles')">
    <section v-for="group in groups" :key="group.area">
      <h2>{{ i18n.t(group.label) }} <span>{{ group.files.length }}</span></h2>
      <p v-if="group.files.length === 0" class="empty-files">{{ i18n.t('changesGroupEmpty') }}</p>
      <ul v-else>
        <li v-for="file in group.files" :key="file.path">
          <button type="button" class="file-change" :class="{ conflicted: file.isConflicted }"
            :aria-pressed="selected?.path === file.path && selected.area === group.area"
            @click="$emit('select', { path: file.path, area: group.area })">
            <code class="change-code">{{ group.area === 'staged' ? file.indexStatus : file.workTreeStatus }}</code>
            <span class="file-name">{{ file.path }}<small v-if="file.originalPath">← {{ file.originalPath }}</small></span>
          </button>
        </li>
      </ul>
    </section>
  </aside>
</template>

<style scoped>
.file-changes-list { min-width: 0; background: var(--gc-surface); border: 1px solid var(--gc-border); border-radius: .5rem; overflow: auto; max-height: 70vh; }
section + section { border-top: 1px solid var(--gc-border); }
h2 { display: flex; justify-content: space-between; gap: .5rem; margin: 0; padding: 1rem; font-size: .95rem; }
h2 span, .empty-files, small { color: var(--gc-text-muted); }
.empty-files { padding: 0 1rem 1rem; font-size: .9rem; }
ul { margin: 0; padding: 0 0 .5rem; list-style: none; }
.file-change { width: 100%; display: flex; gap: .75rem; padding: .65rem 1rem; border: 0; text-align: left; color: var(--gc-text); background: transparent; font: inherit; cursor: pointer; }
.file-change:hover { background: var(--gc-surface-raised); }
.file-change[aria-pressed='true'] { background: var(--gc-selection-bg); box-shadow: inset 3px 0 var(--gc-violet); }
.file-change:focus-visible { outline: 2px solid var(--gc-violet); outline-offset: -2px; }
.change-code { color: var(--gc-warning); }
.conflicted .change-code { color: var(--gc-danger); }
.file-name { min-width: 0; overflow-wrap: anywhere; }
small { display: block; font-size: .8rem; margin-top: .25rem; }
</style>
