<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { RepositoryBranchApiService } from '../../../core/repositories/repository-branch-api.service';
import type { BranchResources } from '../../../core/repositories/models/repository-branch.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import BranchActionDialog from '../repository-branches/BranchActionDialog.vue';
const props = defineProps<{ repositoryId: number; workspaceId: number; conflictCount: number; disabled: boolean }>();
const emit = defineEmits<{ changed: []; busy: [value: boolean] }>();
const i18n = useLanguageStore(); const api = new RepositoryBranchApiService(() => i18n.language, i18n.t);
const resources = ref<BranchResources | null>(null); const error = ref(''); const action = ref<'continue' | 'abort' | null>(null); let controller: AbortController | undefined;
const branch = computed(() => ({ name: resources.value!.currentBranch, sha: '', upstream: null, isCurrent: true, isCheckedOut: true, isRemote: false, remote: '', sourceBranch: '' }));
async function load() {
  controller?.abort(); const current = new AbortController(); controller = current; error.value = ''; action.value = null; resources.value = null;
  try { const response = await api.getResources(props.repositoryId, props.workspaceId, current.signal); if (!current.signal.aborted) { if (!response.result) throw new Error(); resources.value = response.result; } }
  catch (cause) { if (!current.signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('branchError'); }
}
function changed() { action.value = null; emit('busy', false); emit('changed'); void load(); }
function close() { action.value = null; emit('busy', false); }
watch([() => props.repositoryId, () => props.workspaceId, () => props.conflictCount, () => i18n.language], load, { immediate: true });
onUnmounted(() => { controller?.abort(); emit('busy', false); });
</script>
<template>
  <section v-if="resources?.operationState === 'merge' || resources?.operationState === 'rebase'" class="repository-recovery">
    <p>{{ i18n.t('branchPending') }}: <strong>{{ resources.operationState }}</strong> · {{ conflictCount }} {{ i18n.t('conflictRemaining') }}</p>
    <button class="recovery-continue" :disabled="disabled || conflictCount > 0" @click="action = 'continue'">{{ i18n.t('branchContinue') }}</button><button class="recovery-abort" :disabled="disabled" @click="action = 'abort'">{{ i18n.t('branchAbort') }}</button>
  </section>
  <div v-if="error" class="recovery-error"><p role="alert">{{ error }}</p><button @click="load">{{ i18n.t('diffRetry') }}</button></div>
  <BranchActionDialog v-if="action && resources" :repository-id="repositoryId" :workspace-id="workspaceId" :branch="branch" :operation="action" :resources="resources" @busy="$emit('busy', $event)" @close="close" @changed="changed" @uncertain="changed" />
</template>
<style scoped>
.repository-recovery { padding: .6rem; margin-block: .5rem; border: 1px solid var(--gc-warning); background: var(--gc-warning-bg); border-radius: .4rem; }
button { margin-right: .5rem; padding: .4rem .6rem; border: 1px solid var(--gc-border); background: var(--gc-surface); color: var(--gc-text); border-radius: .3rem; }
</style>
