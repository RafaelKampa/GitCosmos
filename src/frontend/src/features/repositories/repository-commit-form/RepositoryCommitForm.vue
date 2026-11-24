<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import Dialog from 'primevue/dialog';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import { RepositoryEditApiService } from '../../../core/repositories/repository-edit-api.service';
import type { GitFileChange } from '../../../core/repositories/models/repository-changes.model';
import type { RepositoryEditPlan, RepositoryEditRequest, RepositoryEditResult, MoveFilesRequest } from '../../../core/repositories/models/repository-edit.model';
const props = defineProps<{ repositoryId: number; workspaceId: number; files: readonly GitFileChange[]; hasCommits: boolean }>();
const emit = defineEmits<{ busy: [value: boolean]; changed: [result: RepositoryEditResult, message: string] }>();
const i18n = useLanguageStore(); const api = new RepositoryEditApiService(() => i18n.language, i18n.t);
const subject = ref(''); const description = ref(''); const amend = ref(false); const options = ref(false);
const busy = ref(false); const error = ref(''); const plan = ref<RepositoryEditPlan | null>(null);
const stagedCount = computed(() => props.files.filter(file => file.indexStatus !== '.' && !file.isConflicted).length);
const blocked = computed(() => busy.value || plan.value !== null);
watch(blocked, value => emit('busy', value));
let request: RepositoryEditRequest | undefined; let controller: AbortController | undefined;
function message(cause: unknown) { return cause instanceof ApiError ? cause.message : i18n.t('editError'); }
function make(operation: RepositoryEditRequest['operation'], paths: string[] = [], push = false): RepositoryEditRequest {
  return { repositoryId: props.repositoryId, workspaceId: props.workspaceId, operation, paths,
    subject: operation === 'commit' ? subject.value.trim() : '', description: operation === 'commit' ? description.value : '', amend: operation === 'commit' && amend.value, push };
}
async function execute(value: RepositoryEditRequest, token: string, signal: AbortSignal) {
  const response = await api.execute(value, token, signal);
  if (signal.aborted) return;
  if (!response.result) throw new ApiError(i18n.t('editError'));
  plan.value = null;
  if (response.result.committed) { subject.value = ''; description.value = ''; amend.value = false; }
  emit('changed', response.result, response.messages.join(' '));
}
async function prepare(value: RepositoryEditRequest) {
  if (blocked.value) return;
  controller = new AbortController(); const signal = controller.signal; busy.value = true; error.value = ''; options.value = false;
  try {
    const response = await api.prepare(value, signal);
    if (signal.aborted) return;
    if (!response.result) throw new ApiError(i18n.t('editError'));
    if (value.operation === 'commit') { request = value; plan.value = response.result; }
    else await execute(value, response.result.token, signal);
  } catch (cause: unknown) { if (!signal.aborted) error.value = message(cause); }
  finally { if (!signal.aborted) busy.value = false; }
}
async function confirm() {
  if (busy.value || !plan.value || !request) return;
  controller = new AbortController(); const signal = controller.signal; busy.value = true; error.value = '';
  try { await execute(request, plan.value.token, signal); }
  catch (cause: unknown) { if (!signal.aborted) { plan.value = null; error.value = message(cause); } }
  finally { if (!signal.aborted) busy.value = false; }
}
function close() { if (!busy.value) plan.value = null; }
function move(value: MoveFilesRequest) { return prepare(make(value.operation, [...value.paths])); }
defineExpose({ move });
onUnmounted(() => controller?.abort());
</script>
<template>
  <form class="repository-commit-form" :aria-busy="busy" @submit.prevent="prepare(make('commit'))" @keydown.esc="options = false">
    <label>{{ i18n.t('commitMessage') }}<input v-model="subject" required maxlength="200" :disabled="blocked" :placeholder="i18n.t('editSubject')" /></label>
    <label>{{ i18n.t('editDescription') }}<textarea v-model="description" maxlength="10000" rows="3" :disabled="blocked" /></label>
    <p v-if="error" role="alert">{{ error }}</p><p v-if="busy" role="status">{{ i18n.t('editBusy') }}</p>
    <div class="commit-controls">
      <label class="amend-option"><input v-model="amend" type="checkbox" :disabled="blocked || !hasCommits" /> Amend</label>
      <span v-if="amend" class="amend-warning">{{ i18n.t('editAmendWarning') }}</span>
      <div class="commit-split">
        <button class="commit-submit" type="submit" :disabled="blocked || !subject.trim() || stagedCount === 0 && !amend">{{ i18n.t(amend ? 'editAmend' : 'editCommit') }} {{ stagedCount }} {{ i18n.t('editFiles') }}</button>
        <button class="commit-options-toggle" type="button" :disabled="blocked" :aria-expanded="options" :aria-label="i18n.t('editOptions')" @click="options = !options">⌄</button>
        <div v-if="options" class="commit-options"><button type="button" :disabled="!subject.trim() || stagedCount === 0 && !amend" @click="prepare(make('commit', [], true))">{{ i18n.t('editCommitPush') }}</button></div>
      </div>
    </div>
  </form>
  <Dialog :visible="plan !== null" modal :closable="!busy" :close-on-escape="!busy" :dismissable-mask="false" :header="i18n.t('editReview')" class="workspace-action-dialog" @update:visible="close">
    <template v-if="plan">
      <p>{{ request?.subject }}</p><pre class="review-description">{{ request?.description }}</pre><p>{{ plan.fileCount }} {{ i18n.t('editFiles') }}</p>
      <p v-if="plan.amend" class="amend-warning">{{ i18n.t('editAmendWarning') }}</p>
      <p v-if="plan.published" class="amend-warning">{{ i18n.t('editPublishedWarning') }}</p>
      <p v-if="plan.pushDestination">{{ i18n.t('editPushDestination') }}: <strong>{{ plan.pushDestination }}</strong></p>
      <ul class="review-files"><li v-for="path in plan.paths" :key="path"><code>{{ path }}</code></li></ul>
      <div class="review-controls"><button type="button" :disabled="busy" @click="close">{{ i18n.t('editCancel') }}</button><button class="commit-confirm" type="button" :disabled="busy" @click="confirm">{{ i18n.t(plan.pushDestination ? 'editCommitPush' : 'editConfirm') }}</button></div>
    </template>
  </Dialog>
</template>
<style scoped>
.repository-commit-form { display: grid; gap: .5rem; padding: .75rem; border-top: 1px solid var(--gc-border); background: var(--gc-surface); }
label { display: grid; gap: .25rem; color: var(--gc-text-muted); font-size: .8rem; }
input:not([type='checkbox']), textarea { width: 100%; min-width: 0; }
textarea { resize: vertical; }
button { padding: .4rem .6rem; color: var(--gc-text); border: 1px solid var(--gc-border); background: var(--gc-surface-raised); border-radius: .3rem; cursor: pointer; }
.commit-controls, .review-controls { display: flex; align-items: center; justify-content: space-between; gap: .5rem; flex-wrap: wrap; }
.amend-option { display: flex; align-items: center; gap: .4rem; }
.amend-warning { color: var(--gc-warning); font-size: .8rem; }
.commit-split { display: flex; position: relative; margin-left: auto; }
.commit-submit { border-radius: .3rem 0 0 .3rem; }
.commit-options-toggle { border-radius: 0 .3rem .3rem 0; }
.commit-options { position: absolute; bottom: calc(100% + .4rem); right: 0; z-index: 3; min-width: 14rem; padding: .35rem; border: 1px solid var(--gc-border); background: var(--gc-surface-raised); border-radius: .3rem; }
.commit-options button { width: 100%; text-align: left; }
.review-files { max-height: 35vh; overflow: auto; overflow-wrap: anywhere; }
.review-description { white-space: pre-wrap; overflow-wrap: anywhere; font: inherit; }
</style>
