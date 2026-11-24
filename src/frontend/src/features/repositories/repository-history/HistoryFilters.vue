<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { RepositoryBranchApiService } from '../../../core/repositories/repository-branch-api.service';
import { ApiError } from '../../../core/api/api-error';
const props = defineProps<{ repositoryId: number; workspaceId: number; branch: string; search: string }>();
const emit = defineEmits<{ apply: [value: { branch: string; search: string }] }>();
const i18n = useLanguageStore(); const api = new RepositoryBranchApiService(() => i18n.language, i18n.t);
const branch = ref(props.branch); const search = ref(props.search); const branches = ref<string[]>([]);
const loading = ref(false); const error = ref(''); let controller: AbortController | undefined;
async function loadBranches() {
  if (loading.value) return;
  controller?.abort(); const current = new AbortController(); controller = current; loading.value = true; error.value = '';
  try {
    const response = await api.getBranches(props.repositoryId, props.workspaceId, current.signal);
    if (!current.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('branchError'));
      branches.value = response.result.branches.map(item => item.name);
    }
  } catch (cause: unknown) { if (!current.signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('branchError'); }
  finally { if (!current.signal.aborted) loading.value = false; }
}
watch([() => props.branch, () => props.search], () => { branch.value = props.branch; search.value = props.search; });
watch([() => props.repositoryId, () => props.workspaceId, () => i18n.language], () => { controller?.abort(); branches.value = []; loading.value = false; error.value = ''; });
onUnmounted(() => controller?.abort());
</script>
<template>
  <form class="history-filters" @submit.prevent="emit('apply', { branch, search: search.trim() })">
    <label>{{ i18n.t('historyBranchFilter') }}<select v-model="branch" :aria-busy="loading" @focus="loadBranches"><option value="">{{ i18n.t('historyAllBranches') }}</option><option v-if="branch && !branches.includes(branch)" :value="branch">{{ branch }}</option><option v-for="name in branches" :key="name" :value="name">{{ name }}</option></select></label>
    <label class="history-search-field">{{ i18n.t('historySearch') }}<input v-model="search" type="search" maxlength="200" :placeholder="i18n.t('historySearchHint')" /></label>
    <button type="submit">{{ i18n.t('historyApply') }}</button>
    <p v-if="error" role="alert">{{ error }} <button class="history-branches-retry" type="button" @click="loadBranches">{{ i18n.t('diffRetry') }}</button></p>
  </form>
</template>
<style scoped>
.history-filters { display: flex; gap: .7rem; flex-wrap: wrap; align-items: end; margin-bottom: .75rem; }
label { display: grid; gap: .3rem; color: var(--gc-text-muted); font-size: .8rem; min-width: 0; }
.history-search-field { flex: 1; min-width: 12rem; }
select { max-width: 100%; }
button { color: var(--gc-text); background: var(--gc-surface); border: 1px solid var(--gc-border); border-radius: .4rem; padding: .6rem .75rem; }
.history-filters > p { flex-basis: 100%; margin: 0; }
</style>
