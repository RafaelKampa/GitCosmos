<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import Dialog from 'primevue/dialog';
import DiffViewer from '../diff-viewer/DiffViewer.vue';
import LocalFileMenu from '../local-file-menu/LocalFileMenu.vue';
import type { FileDiff } from '../../../core/repositories/models/repository-changes.model';
import type { DiffTextContext } from '../../../core/repositories/models/local-files.model';
import type { LinesSnapshot, LinesPlan, PrepareLines } from '../../../core/repositories/models/repository-lines.model';
import { RepositoryLinesApiService } from '../../../core/repositories/repository-lines-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
const props = defineProps<{ repositoryId: number; workspaceId: number; diff: FileDiff; disabled?: boolean }>();
const emit = defineEmits<{ busy: [value: boolean]; changed: []; uncertain: [] }>();
const i18n = useLanguageStore(); const api = new RepositoryLinesApiService(() => i18n.language, i18n.t);
const snapshot = ref<LinesSnapshot | null>(null); const selected = ref<number[]>([]); const loading = ref(false); const busy = ref(false); const error = ref(''); const uncertain = ref(false); const menuBusy = ref(false);
const plan = ref<LinesPlan | null>(null); const menu = ref<DiffTextContext | null>(null); const menuRevision = ref(0);
let reviewed: PrepareLines | undefined; let controller: AbortController | undefined;
const blocked = computed(() => loading.value || busy.value || menuBusy.value || plan.value !== null);
watch(blocked, value => emit('busy', value));
function reset() { controller?.abort(); snapshot.value = null; selected.value = []; plan.value = null; reviewed = undefined; loading.value = false; busy.value = false; error.value = ''; uncertain.value = false; menu.value = null; menuBusy.value = false; }
function failure(cause: unknown) { return cause instanceof ApiError ? cause.message : i18n.t('linesError'); }
async function load() {
  if (blocked.value || props.disabled) return;
  reset(); if (props.diff.kind !== 'text') return;
  const current = new AbortController(); controller = current; loading.value = true;
  try { const response = await api.read({ repositoryId: props.repositoryId, workspaceId: props.workspaceId, path: props.diff.path, area: props.diff.area }, current.signal); if (!current.signal.aborted) { if (!response.result) throw new Error(); snapshot.value = response.result; } }
  catch (cause) { if (!current.signal.aborted) error.value = failure(cause); }
  finally { if (!current.signal.aborted) loading.value = false; }
}
async function execute(request: PrepareLines, value: LinesPlan, current: AbortController) {
  try {
    const response = await api.execute(request, value.token, current.signal);
    if (!current.signal.aborted) { if (response.result !== true) throw new Error(); plan.value = null; snapshot.value = null; selected.value = []; emit('changed'); }
  } catch (cause) { if (!current.signal.aborted) { plan.value = null; uncertain.value = true; emit('uncertain'); } throw cause; }
}
async function prepare(operation: 'move' | 'discard', ids: number[]) {
  if (blocked.value || props.disabled || uncertain.value || !snapshot.value?.canSelect || !ids.length) return;
  const request: PrepareLines = { file: { repositoryId: props.repositoryId, workspaceId: props.workspaceId, path: props.diff.path, area: props.diff.area }, lines: [...ids], token: snapshot.value.token, operation };
  const current = new AbortController(); controller = current; busy.value = true; error.value = '';
  try {
    const response = await api.prepare(request, current.signal);
    if (!current.signal.aborted) { if (!response.result) throw new Error(); if (operation === 'discard') { reviewed = request; plan.value = response.result; } else await execute(request, response.result, current); }
  } catch (cause) { if (!current.signal.aborted) error.value = failure(cause); }
  finally { if (!current.signal.aborted) busy.value = false; }
}
async function confirm() {
  if (busy.value || !reviewed || !plan.value) return;
  const current = new AbortController(); controller = current; busy.value = true; error.value = '';
  try { await execute(reviewed, plan.value, current); }
  catch (cause) { if (!current.signal.aborted) error.value = failure(cause); }
  finally { if (!current.signal.aborted) busy.value = false; }
}
function close() { if (!busy.value) plan.value = null; }
function context(value: DiffTextContext) { if (!blocked.value) { menu.value = value; menuRevision.value++; } }
watch([() => props.repositoryId, () => props.workspaceId, () => props.diff, () => i18n.language], () => { reset(); void load(); }, { immediate: true });
watch(() => props.disabled, value => { if (!value && !snapshot.value && !blocked.value) void load(); });
onUnmounted(() => { controller?.abort(); emit('busy', false); });
</script>
<template>
  <section class="line-staging" :aria-busy="blocked">
    <div v-if="error || uncertain || snapshot && !snapshot.canSelect" class="lines-toolbar"><button class="lines-start" :disabled="blocked || disabled" @click="load">{{ i18n.t('dashboardRefresh') }}</button></div>
    <p v-if="loading" role="status">{{ i18n.t('diffLoading') }}</p><p v-if="error" role="alert">{{ error }}</p>
    <p v-if="snapshot && !snapshot.canSelect" role="status">{{ snapshot.reason }}</p><p v-if="uncertain" role="alert">{{ i18n.t('linesUncertain') }}</p>
    <DiffViewer :diff="snapshot?.canSelect ? snapshot.diff : diff" :selectable="snapshot?.canSelect" :selected="selected" :disabled="blocked || disabled || uncertain" @selection="selected = $event" @action="prepare" @context="context" />
    <LocalFileMenu v-if="menu" :key="menuRevision" :repository-id="repositoryId" :workspace-id="workspaceId" :selection="menu" :snapshot-token="snapshot?.token" :context="{ path: diff.path, area: diff.area, paths: [diff.path], all: [diff.path], x: menu.x, y: menu.y }" @close="menu = null" @busy="menuBusy = $event" />
  </section>
  <Dialog v-if="plan" visible modal :closable="!busy" :close-on-escape="!busy" :header="i18n.t('localDiscard')" class="workspace-action-dialog" @update:visible="close">
    <p><code>{{ plan.path }}</code> · {{ plan.lineCount }} {{ i18n.t('linesCount') }}</p><p>{{ i18n.t('localDiscardLinesWarning') }}</p>
    <p v-if="plan.removesEntry">{{ i18n.t('localDeleteFileWarning') }}</p><template v-else><h3>{{ i18n.t('localWorkingResult') }}</h3><pre class="lines-preview">{{ plan.result }}</pre></template>
    <button class="lines-cancel" :disabled="busy" @click="close">{{ i18n.t('editCancel') }}</button><button class="lines-confirm" :disabled="busy" @click="confirm">{{ i18n.t('localDiscard') }}</button>
  </Dialog>
</template>
<style scoped>
.line-staging { display: flex; flex-direction: column; min-height: 0; flex: 1; }
.lines-toolbar { padding: .5rem; }
button { background: var(--gc-surface-raised); color: var(--gc-text); border: 1px solid var(--gc-border); border-radius: .3rem; padding: .4rem .6rem; }
p { padding-inline: .75rem; font-size: .85rem; color: var(--gc-text-muted); }
[role='alert'] { color: var(--gc-danger); }
.lines-preview { overflow: auto; max-height: 50vh; max-width: 75vw; background: var(--gc-bg); padding: .75rem; }
</style>
