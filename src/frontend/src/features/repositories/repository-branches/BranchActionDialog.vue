<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import Dialog from 'primevue/dialog';
import { RepositoryBranchApiService } from '../../../core/repositories/repository-branch-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { BranchItem, BranchOperation, BranchResources, RepositoryBranchPlan, RepositoryBranchRequest, RebaseStep } from '../../../core/repositories/models/repository-branch.model';
import { branchActionLabels } from './branch-action-labels';
const props = defineProps<{ repositoryId: number; workspaceId: number; branch: BranchItem; operation: BranchOperation; resources: BranchResources }>();
const emit = defineEmits<{ close: []; changed: [message: string]; uncertain: []; busy: [value: boolean] }>();
const i18n = useLanguageStore(); const api = new RepositoryBranchApiService(() => i18n.language, i18n.t);
const name = ref(props.operation === 'checkoutRemote' || props.operation === 'worktree' ? props.branch.sourceBranch : '');
const remote = ref(props.branch.remote || props.resources.remotes[0] || ''); const remoteBranch = ref(props.branch.sourceBranch);
const destination = ref(''); const message = ref(''); const upstream = ref(props.branch.upstream || ''); const baseBranch = ref(props.resources.currentBranch); const confirmation = ref('');
const busy = ref(false); const error = ref(''); const plan = ref<RepositoryBranchPlan | null>(null); const steps = ref<RebaseStep[]>([]);
const needsName = computed(() => ['create', 'rename', 'tag', 'checkoutRemote'].includes(props.operation) || props.operation === 'worktree' && props.branch.isRemote);
const needsRemote = computed(() => ['push', 'pullRequest', 'fetch'].includes(props.operation) && !props.branch.isRemote);
let controller: AbortController | undefined; let reviewed: RepositoryBranchRequest | undefined;
watch(busy, value => emit('busy', value));
function failure(cause: unknown) { return cause instanceof ApiError ? cause.message : i18n.t('branchError'); }
function request(): RepositoryBranchRequest {
  const basic: RepositoryBranchRequest = { repositoryId: props.repositoryId, workspaceId: props.workspaceId, operation: props.operation, name: props.branch.name, newName: needsName.value ? name.value.trim() : '' };
  if (['checkout', 'create', 'rename', 'delete'].includes(props.operation) && !props.branch.isRemote) return basic;
  return { ...basic, isRemote: props.branch.isRemote, remote: remote.value, remoteBranch: remoteBranch.value, destination: destination.value, message: message.value, upstream: upstream.value, baseBranch: baseBranch.value, confirmation: confirmation.value };
}
async function prepare() {
  if (busy.value || plan.value) return;
  controller = new AbortController(); const signal = controller.signal; busy.value = true; error.value = '';
  try {
    const value = request(); const response = await api.prepare(value, signal);
    if (!signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('branchError'));
      reviewed = value; plan.value = response.result;
      steps.value = (response.result.commits || []).map(commit => ({ sha: commit.sha, action: 'pick' }));
    }
  } catch (cause: unknown) { if (!signal.aborted) error.value = failure(cause); }
  finally { if (!signal.aborted) busy.value = false; }
}
async function confirm() {
  if (busy.value || !plan.value || !reviewed) return;
  controller = new AbortController(); const signal = controller.signal; busy.value = true; error.value = ''; let executing = false;
  try {
    let value = reviewed; let token = plan.value.token;
    if (props.operation === 'interactiveRebase') {
      value = { ...reviewed, steps: steps.value.map(step => ({ ...step })) };
      const response = await api.prepare(value, signal);
      if (signal.aborted) return;
      if (!response.result || response.result.stateToken !== plan.value.stateToken) throw new ApiError(i18n.t('branchStale'));
      token = response.result.token;
    }
    executing = true;
    const response = await api.execute(value, token, signal);
    if (!signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('branchError'));
      emit('changed', response.messages.join(' ')); emit('close');
    }
  } catch (cause: unknown) { if (!signal.aborted) { error.value = failure(cause); plan.value = null; if (executing) emit('uncertain'); } }
  finally { if (!signal.aborted) busy.value = false; }
}
function close() { if (!busy.value) emit('close'); }
function move(index: number, offset: number) { const next = [...steps.value]; const target = index + offset; if (target < 0 || target >= next.length) return; [next[index], next[target]] = [next[target]!, next[index]!]; steps.value = next; }
onUnmounted(() => controller?.abort());
</script>
<template>
  <Dialog visible modal :closable="!busy" :close-on-escape="!busy" :dismissable-mask="false" :header="i18n.t(branchActionLabels[operation])" class="workspace-action-dialog" @update:visible="close">
    <p v-if="!['fetch', 'continue', 'abort'].includes(operation)"><strong>{{ i18n.t('gitBranch') }}:</strong> {{ branch.name }}</p>
    <p v-if="plan ? plan.currentBranch : resources.currentBranch">{{ i18n.t('branchCurrentTarget') }}: <strong>{{ plan ? plan.currentBranch : resources.currentBranch }}</strong></p>
    <p v-if="error" role="alert">{{ error }}</p>
    <form v-if="!plan" class="branch-action-form" @submit.prevent="prepare">
      <label v-if="needsName">{{ i18n.t('branchNewName') }}<input v-model="name" class="branch-new-name" required maxlength="255" :disabled="busy" /></label>
      <label v-if="needsRemote">{{ i18n.t('branchRemote') }}<select v-model="remote" class="branch-remote" required :disabled="busy"><option v-for="item in resources.remotes" :key="item" :value="item">{{ item }}</option></select></label>
      <label v-if="operation === 'push' || operation === 'pullRequest' && !branch.isRemote">{{ i18n.t('branchRemoteBranch') }}<input v-model="remoteBranch" class="branch-remote-name" required maxlength="255" :disabled="busy" /></label>
      <label v-if="operation === 'pullRequest'">{{ i18n.t('branchBase') }}<input v-model="baseBranch" class="branch-base" required maxlength="255" :disabled="busy" /></label>
      <label v-if="operation === 'worktree'">{{ i18n.t('branchDestination') }}<input v-model="destination" class="branch-destination" required :disabled="busy" /></label>
      <label v-if="operation === 'tag'">{{ i18n.t('branchTagMessage') }}<textarea v-model="message" maxlength="10000" :disabled="busy" /></label>
      <label v-if="operation === 'setUpstream' || operation === 'fastForward'">{{ i18n.t(operation === 'fastForward' ? 'branchTarget' : 'branchSetUpstream') }}<select v-model="upstream" class="branch-upstream" required :disabled="busy"><option v-for="item in resources.branches" :key="item.name" :value="'refs/remotes/' + item.name">{{ item.name }}</option></select></label>
      <label v-if="operation === 'deleteRemote'">{{ i18n.t('branchDeleteConfirm') }}<input v-model="confirmation" class="branch-delete-confirmation" required :placeholder="branch.name" :disabled="busy" /></label>
      <div class="branch-dialog-controls"><button type="button" :disabled="busy" @click="close">{{ i18n.t('editCancel') }}</button><button class="branch-review" type="submit" :disabled="busy">{{ i18n.t('branchReview') }}</button></div>
    </form>
    <section v-else class="branch-dialog">
      <p><code>{{ plan.sha }}</code><span v-if="plan.newName"> → {{ plan.newName }}</span></p>
      <p v-if="plan.target">{{ i18n.t('branchTarget') }}: <strong>{{ plan.target }}</strong></p>
      <p v-if="operation === 'checkoutRemote' && plan.target">{{ i18n.t('branchCheckoutExisting') }}</p>
      <p v-if="reviewed?.remoteBranch">{{ i18n.t('branchRemoteBranch') }}: {{ reviewed.remoteBranch }}</p>
      <code v-if="plan.targetSha">{{ plan.targetSha }}</code><pre v-if="reviewed?.message">{{ reviewed.message }}</pre>
      <p>{{ i18n.t('branchOperationNotice') }}</p>
      <p v-if="plan.rewritesHistory" class="branch-warning">{{ i18n.t('branchRebaseWarning') }}</p>
      <p v-if="plan.published" class="branch-warning">{{ i18n.t('branchPublishedWarning') }}</p>
      <p v-if="['push', 'pull', 'fetch', 'deleteRemote'].includes(operation)" class="branch-warning">{{ i18n.t('branchNetworkWarning') }}</p>
      <p v-if="operation === 'delete' || operation === 'deleteRemote'" class="branch-warning">{{ i18n.t(operation === 'deleteRemote' ? 'branchRemoteDeleteWarning' : 'branchDeleteNotice') }}</p>
      <p v-if="operation === 'abort'" class="branch-warning">{{ i18n.t('branchAbortWarning') }}</p>
      <ol v-if="operation === 'interactiveRebase'" class="rebase-steps"><li v-for="(step, index) in steps" :key="step.sha">
        <button type="button" :disabled="busy || index === 0" :aria-label="i18n.t('branchMoveUp')" @click="move(index, -1)">↑</button><button type="button" :disabled="busy || index === steps.length - 1" :aria-label="i18n.t('branchMoveDown')" @click="move(index, 1)">↓</button>
        <select v-model="step.action" :disabled="busy" :aria-label="i18n.t('branchAction')"><option value="pick">Pick</option><option value="drop">Drop</option><option value="squash">Squash</option><option value="fixup">Fixup</option></select>
        <code>{{ step.sha.slice(0, 8) }}</code> {{ plan.commits?.find(commit => commit.sha === step.sha)?.subject }}
      </li></ol>
      <p v-if="plan.url">{{ i18n.t('branchPrNotice') }} <a class="branch-pr-link" :href="plan.url" target="_blank" rel="noopener noreferrer">{{ i18n.t('branchOpenPr') }}</a></p>
      <div class="branch-dialog-controls"><button type="button" :disabled="busy" @click="close">{{ i18n.t('editCancel') }}</button><button v-if="!plan.url" class="branch-confirm" type="button" :disabled="busy" @click="confirm">{{ i18n.t('branchConfirm') }}</button></div>
    </section>
  </Dialog>
</template>
<style scoped>
.branch-action-form, label { display: grid; gap: .4rem; }
.branch-action-form { gap: .8rem; }
.branch-dialog-controls { display: flex; justify-content: flex-end; gap: .5rem; margin-top: 1rem; }
button { color: var(--gc-text); background: var(--gc-surface-raised); border: 1px solid var(--gc-border); border-radius: .3rem; padding: .4rem .65rem; }
.branch-warning { color: var(--gc-warning); }
.rebase-steps { max-height: 40vh; overflow-y: auto; padding-left: 1.5rem; }
.rebase-steps li { margin-block: .4rem; overflow-wrap: anywhere; }
.rebase-steps select { margin-inline: .4rem; }
.branch-dialog code { overflow-wrap: anywhere; }
</style>
