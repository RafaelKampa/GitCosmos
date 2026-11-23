<script setup lang="ts">
import { computed } from 'vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { CatalogRepository } from '../../../core/catalog/catalog.model';
const props = defineProps<{ repositories: readonly CatalogRepository[]; loading?: boolean }>();
const i18n = useLanguageStore();
const metrics = computed(() => [
  { key: 'repositoriesMetric' as const, value: props.repositories.length, tone: 'total' },
  { key: 'modifiedMetric' as const, value: props.repositories.filter(r => r.state === 'modified' || r.state === 'conflicted').length, tone: 'modified' },
  { key: 'behindMetric' as const, value: props.repositories.filter(r => (r.status?.behind ?? 0) > 0).length, tone: 'behind' },
  { key: 'aheadMetric' as const, value: props.repositories.filter(r => (r.status?.ahead ?? 0) > 0).length, tone: 'ahead' },
]);
</script>
<template>
  <dl class="summary-metrics">
    <div v-for="metric in metrics" :key="metric.key" class="metric-tile" :class="metric.tone">
      <dt>{{ i18n.t(metric.key) }}</dt><dd>{{ loading ? '—' : metric.value }}</dd>
    </div>
  </dl>
</template>
