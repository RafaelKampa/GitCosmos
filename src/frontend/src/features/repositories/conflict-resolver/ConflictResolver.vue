<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import Dialog from 'primevue/dialog';
import { RepositoryConflictApiService } from '../../../core/repositories/repository-conflict-api.service';
import type { RepositoryConflict, ResolutionAction, ResolveConflictRequest, ConflictResolutionPlan } from '../../../core/repositories/models/repository-conflict.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
const props = defineProps<{ repositoryId: number; workspaceId: number; path: string }>();
const emit = defineEmits<{ busy: [value: boolean]; resolved: []; uncertain: [] }>();
const i18n = useLanguageStore(); const api = new RepositoryConflictApiService(() => i18n.language, i18n.t);
const conflict = ref<RepositoryConflict | null>(null); const content = ref(''); const action = ref<ResolutionAction>('edit'); const loading = ref(false); const busy = ref(false); const error = ref(''); const externalNotice = ref(false); const uncertain = ref(false);
const plan = ref<ConflictResolutionPlan | null>(null); let reviewed: ResolveConflictRequest | undefined; let controller: AbortController | undefined;
const blocked = computed(() => busy.value || plan.value !== null);
const editable = computed(() => conflict.value?.working.kind === 'text' || conflict.value?.working.kind === 'absent');
const versions = computed(() => conflict.value ? [{ key: 'base' as const, value: conflict.value.base }, { key: 'ours' as const, value: conflict.value.ours }, { key: 'theirs' as const, value: conflict.value.theirs }] : []);
const labels = { base: 'conflictBase', ours: 'conflictOurs', theirs: 'conflictTheirs' } as const;
const kinds = { absent: 'conflictAbsent', binary: 'diffBinary', tooLarge: 'diffTooLarge', text: 'conflictResult' } as const;
watch(blocked, value => emit('busy', value));
function file() { return { repositoryId: props.repositoryId, workspaceId: props.workspaceId, path: props.path }; }
function failure(cause: unknown) { return cause instanceof ApiError ? cause.message : i18n.t('conflictError'); }
async function load() {
  controller?.abort(); const current = new AbortController(); controller = current; loading.value = true; busy.value = false; plan.value = null; conflict.value = null; error.value = ''; externalNotice.value = false; uncertain.value = false;
  try { const response = await api.read(file(), current.signal); if (!current.signal.aborted) { if (!response.result) throw new Error(); conflict.value = response.result; content.value = response.result.working.text; action.value = editable.value ? 'edit' : 'stage'; } }
  catch (cause) { if (!current.signal.aborted) error.value = failure(cause); }
  finally { if (!current.signal.aborted) loading.value = false; }
}
async function prepare() {
  if (blocked.value || !conflict.value || uncertain.value) return;
  controller?.abort(); const current = new AbortController(); controller = current; busy.value = true; error.value = '';
  const request: ResolveConflictRequest = { file: file(), action: action.value, content: action.value === 'edit' ? content.value : '', token: conflict.value.token };
  try { const response = await api.prepare(request, current.signal); if (!current.signal.aborted) { if (!response.result) throw new Error(); reviewed = request; plan.value = response.result; } }
  catch (cause) { if (!current.signal.aborted) error.value = failure(cause); }
  finally { if (!current.signal.aborted) busy.value = false; }
}
async function confirm() {
  if (busy.value || !plan.value || !reviewed) return;
  controller?.abort(); const current = new AbortController(); controller = current; busy.value = true; error.value = '';
  try { const response = await api.resolve({ ...reviewed, token: plan.value.token }, current.signal); if (!current.signal.aborted) { if (response.result !== true) throw new Error(); plan.value = null; emit('resolved'); } }
  catch (cause) { if (!current.signal.aborted) { error.value = failure(cause); plan.value = null; uncertain.value = true; emit('uncertain'); } }
  finally { if (!current.signal.aborted) busy.value = false; }
}
async function external() {
  if (blocked.value) return;
  controller?.abort(); const current = new AbortController(); controller = current; busy.value = true; error.value = '';
  try { const response = await api.external(file(), current.signal); if (!current.signal.aborted) { if (response.result !== true) throw new Error(); externalNotice.value = true; } }
  catch (cause) { if (!current.signal.aborted) error.value = failure(cause); }
  finally { if (!current.signal.aborted) busy.value = false; }
}
function close() { if (!busy.value) plan.value = null; }
watch([() => props.repositoryId, () => props.workspaceId, () => props.path, () => i18n.language], load, { immediate: true });
onUnmounted(() => { controller?.abort(); emit('busy', false); });
</script>
<template>
  <section class="conflict-resolver" :aria-label="i18n.t('conflictTitle')" :aria-busy="loading || busy">
    <header><h2>{{ i18n.t('conflictTitle') }}</h2><button class="conflict-refresh" :disabled="blocked" @click="load">{{ i18n.t('dashboardRefresh') }}</button></header>
    <p v-if="loading" role="status">{{ i18n.t('changesLoading') }}</p><p v-if="error" role="alert">{{ error }}</p>
    <template v-if="conflict">
      <p>{{ i18n.t('conflictExplanation') }}</p><p v-if="conflict.operation === 'rebase'" class="conflict-warning">{{ i18n.t('conflictRebaseWarning') }}</p>
      <div class="conflict-versions"><section v-for="version in versions" :key="version.key"><h3>{{ i18n.t(labels[version.key]) }}</h3><code>{{ version.value.sha.slice(0, 12) }}</code><pre v-if="version.value.kind === 'text'">{{ version.value.text }}</pre><p v-else>{{ i18n.t(kinds[version.value.kind]) }}</p></section></div>
      <label>{{ i18n.t('conflictResolution') }}<select v-model="action" class="conflict-action" :disabled="blocked || uncertain"><option value="edit" :disabled="!editable">{{ i18n.t('conflictEdit') }}</option><option value="ours">{{ i18n.t('conflictUseOurs') }}</option><option value="theirs">{{ i18n.t('conflictUseTheirs') }}</option><option value="stage">{{ i18n.t('conflictStageCurrent') }}</option><option value="delete">{{ i18n.t('conflictDelete') }}</option></select></label>
      <label v-if="action === 'edit'">{{ i18n.t('conflictResult') }}<textarea v-model="content" class="conflict-result" rows="14" maxlength="200000" :disabled="blocked || uncertain" spellcheck="false" /></label>
      <p v-else-if="action === 'stage'">{{ i18n.t('conflictStageNotice') }}</p>
      <p v-if="externalNotice" role="status">{{ i18n.t('conflictExternalNotice') }}</p><p v-if="uncertain" class="conflict-warning">{{ i18n.t('conflictUncertain') }}</p>
      <div class="conflict-actions"><button class="conflict-external" :disabled="blocked || conflict.working.kind === 'absent'" @click="external">{{ i18n.t('conflictExternal') }}</button><button class="conflict-prepare" :disabled="blocked || uncertain" @click="prepare">{{ i18n.t('conflictReview') }}</button></div>
    </template>
  </section>
  <Dialog v-if="plan" visible modal :header="i18n.t('conflictReview')" :closable="!busy" :close-on-escape="!busy" class="workspace-action-dialog" @update:visible="close">
    <p><code>{{ plan.path }}</code></p><p>{{ i18n.t('conflictReviewWarning') }}</p><p v-if="plan.removesFile">{{ i18n.t('conflictDeleteWarning') }}</p><pre v-else-if="plan.kind === 'text'" class="conflict-preview">{{ plan.text }}</pre><p v-else>{{ i18n.t('conflictBinaryReview') }}</p>
    <button class="conflict-cancel" :disabled="busy" @click="close">{{ i18n.t('editCancel') }}</button><button class="conflict-confirm" :disabled="busy" @click="confirm">{{ i18n.t('conflictConfirm') }}</button><p v-if="error" role="alert">{{ error }}</p>
  </Dialog>
</template>
<style scoped>
.conflict-resolver { padding: .75rem; overflow: auto; }
header, .conflict-actions { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: .5rem; }
h2 { font-size: 1rem; } h3 { font-size: .85rem; }
button { background: var(--gc-surface-raised); color: var(--gc-text); border: 1px solid var(--gc-border); border-radius: .3rem; padding: .45rem .6rem; }
.conflict-versions { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .5rem; margin-block: .75rem; }
.conflict-versions section { min-width: 0; border: 1px solid var(--gc-border); padding: .5rem; border-radius: .3rem; }
.conflict-versions pre { overflow: auto; max-height: 18rem; font-size: .75rem; }
.conflict-versions code { color: var(--gc-text-muted); font-size: .7rem; }
label { display: grid; gap: .35rem; margin-block: .75rem; }
.conflict-result { width: 100%; resize: vertical; font-family: monospace; white-space: pre; }
.conflict-warning { color: var(--gc-warning); }
.conflict-preview { max-height: 45vh; max-width: 75vw; overflow: auto; }
@media (max-width: 800px) { .conflict-versions { grid-template-columns: minmax(0, 1fr); } }
</style>
