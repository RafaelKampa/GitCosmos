<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import FolderPathField from '../../../shared/components/folder-path-field/FolderPathField.vue';
import { RepositoryCloneApiService } from '../../../core/workspaces/repository-clone-api.service';
import type { RepositoryClonePlan } from '../../../core/workspaces/models/requests/repository-clone.model';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';

const props = defineProps<{ workspaceId: number; disabled: boolean }>();
const emit = defineEmits<{ busy: [value: boolean]; changed: [] }>();
const i18n = useLanguageStore();
const api = new RepositoryCloneApiService(() => i18n.language, i18n.t);
const remoteUrl = ref('');
const parentPath = ref('');
const plan = ref<RepositoryClonePlan | null>(null);
const reviewing = ref(false);
const cloning = ref(false);
const selecting = ref(false);
const error = ref('');
const message = ref('');
const busy = computed(() => reviewing.value || cloning.value || selecting.value);
let previewRequest: AbortController | undefined;
let cloneRequest: AbortController | undefined;
let disposed = false;
watch(busy, value => emit('busy', value));
watch([remoteUrl, parentPath], () => { previewRequest?.abort(); reviewing.value = false; plan.value = null; });

function showError(cause: unknown) { error.value = cause instanceof ApiError ? cause.message : i18n.t('cloneError'); }
async function review() {
  if (busy.value || props.disabled) return;
  const request = new AbortController();
  previewRequest = request;
  reviewing.value = true; error.value = ''; message.value = ''; plan.value = null;
  try {
    const response = await api.preview({ workspaceId: props.workspaceId, remoteUrl: remoteUrl.value.trim(), parentPath: parentPath.value.trim() }, request.signal);
    if (!request.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('cloneError'));
      plan.value = response.result;
    }
  } catch (cause: unknown) { if (!request.signal.aborted) showError(cause); }
  finally { if (!request.signal.aborted) reviewing.value = false; }
}
async function confirm() {
  if (busy.value || props.disabled || !plan.value) return;
  const confirmed = plan.value;
  const request = new AbortController();
  cloneRequest = request;
  cloning.value = true; error.value = ''; message.value = '';
  try {
    const response = await api.clone({ workspaceId: props.workspaceId, remoteUrl: confirmed.remoteUrl,
      parentPath: confirmed.parentPath, destinationPath: confirmed.destinationPath }, request.signal);
    if (!request.signal.aborted) { message.value = response.messages.join(' '); plan.value = null; emit('changed'); }
    else if (!disposed) message.value = i18n.t('cloneCancelled');
  } catch (cause: unknown) {
    if (request.signal.aborted) { if (!disposed) message.value = i18n.t('cloneCancelled'); }
    else showError(cause);
  } finally { if (!disposed) cloning.value = false; }
}
function cancel() { cloneRequest?.abort(); }
onUnmounted(() => { disposed = true; previewRequest?.abort(); cloneRequest?.abort(); });
</script>

<template>
  <div class="repository-clone-form">
    <form @submit.prevent="review">
      <label for="clone-repository-url">{{ i18n.t('cloneUrl') }}</label>
      <InputText id="clone-repository-url" v-model="remoteUrl" required maxlength="4096" autocomplete="off" :disabled="busy || disabled" />
      <FolderPathField id="clone-parent-path" v-model="parentPath" :label="i18n.t('cloneParentPath')" :help="i18n.t('clonePathHelp')"
        :disabled="reviewing || cloning || disabled" @busy="selecting = $event" @error="error = $event" />
      <Button type="submit" :label="i18n.t(reviewing ? 'cloneReviewing' : 'cloneReview')" :disabled="busy || disabled" />
    </form>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="message" role="status">{{ message }}</p>
    <section v-if="plan" class="clone-preview" :aria-label="i18n.t('cloneDestination')">
      <p>{{ i18n.t('cloneWillCreate') }}</p>
      <code>{{ plan.destinationPath }}</code>
      <Button type="button" class="clone-confirm" :label="i18n.t('cloneConfirm')" :disabled="busy || disabled" @click="confirm" />
    </section>
    <div v-if="cloning" class="clone-progress">
      <p role="status">{{ i18n.t('cloningRepository') }}</p>
      <Button type="button" class="clone-cancel" severity="secondary" :label="i18n.t('cloneCancel')" @click="cancel" />
    </div>
  </div>
</template>

<style scoped>
.repository-clone-form, form, .clone-preview { display: grid; gap: 1rem; }
.clone-preview { padding: 1rem; border: 1px solid var(--gc-border); border-radius: .5rem; background: var(--gc-surface); }
.clone-preview code { overflow-wrap: anywhere; color: var(--gc-text-muted); }
</style>
