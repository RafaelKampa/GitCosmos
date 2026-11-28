<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import Dialog from 'primevue/dialog';
import type { LocalFileContext, DiffTextContext, LocalFilesRequest, LocalFilesView, LocalFilesPlan } from '../../../core/repositories/models/local-files.model';
import { LocalFilesApiService } from '../../../core/repositories/local-files-api.service';
import { RepositoryLinesApiService } from '../../../core/repositories/repository-lines-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';
import { ApiError } from '../../../core/api/api-error';
const props = defineProps<{ repositoryId: number; workspaceId: number; context: LocalFileContext; selection?: DiffTextContext; snapshotToken?: string; disabled?: boolean; untracked?: boolean }>();
const emit = defineEmits<{ close: []; busy: [value: boolean]; changed: []; uncertain: []; move: [value: { operation: 'stage' | 'unstage'; paths: string[] }] }>();
const i18n = useLanguageStore(); const preferences = useExplorerPreferences();
const api = new LocalFilesApiService(() => i18n.language, i18n.t); const linesApi = new RepositoryLinesApiService(() => i18n.language, i18n.t);
const popup = ref<HTMLElement>(); const opened = ref(true); const busy = ref(false); const error = ref(''); const view = ref<LocalFilesView | null>(null); const plan = ref<LocalFilesPlan | null>(null); const dialog = ref(''); const uncertain = ref(false);
let reviewed: LocalFilesRequest | undefined; const controller = new AbortController();
const position = computed(() => {
  const top = Math.max(8, Math.min(props.context.y, window.innerHeight - (props.selection ? 220 : 540)));
  return { left: Math.max(8, Math.min(props.context.x, window.innerWidth - 280)) + 'px', top: top + 'px', maxHeight: Math.max(100, window.innerHeight - top - 8) + 'px' };
});
const formatter = computed(() => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'short', timeStyle: 'medium' }));
function request(action: string, paths = [props.context.path]): LocalFilesRequest { return { repositoryId: props.repositoryId, workspaceId: props.workspaceId, paths, area: props.context.area, action, ...(props.selection ? { start: props.selection.start, end: props.selection.end } : {}) }; }
function close() { if (busy.value) return; opened.value = false; dialog.value = ''; plan.value = null; reviewed = undefined; emit('busy', false); emit('close'); }
async function task(work: () => Promise<void>) {
  if (busy.value || props.disabled || uncertain.value) return; busy.value = true; emit('busy', true); error.value = '';
  try { await work(); } catch (cause) { if (!controller.signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('localError'); }
  finally { if (!controller.signal.aborted) { busy.value = false; emit('busy', plan.value !== null); if (!opened.value && !dialog.value && !error.value) emit('close'); } }
}
function download(patch: string) { const url = URL.createObjectURL(new Blob([patch], { type: 'text/plain;charset=utf-8' })); const link = document.createElement('a'); link.href = url; link.download = 'gitcosmos-changes.patch'; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); }
async function run(action: string, option = '') {
  if (action === 'move' || action === 'all') { if (!props.disabled && !busy.value) { emit('move', { operation: props.context.area === 'staged' ? 'unstage' : 'stage', paths: action === 'all' ? props.context.all : props.context.paths }); close(); } return; }
  await task(async () => {
    const signal = controller.signal;
    if (action === 'copy' || action === 'full' || action === 'copySelection') {
      const text = action === 'copySelection' ? props.selection!.text : action === 'copy' ? props.context.paths.join('\n') : props.context.paths.map(path => { const full = view.value!.fullPath; const separator = full.includes('\\') ? '\\' : '/'; return full.slice(0, -props.context.path.length) + path.replaceAll('/', separator); }).join('\n');
      await navigator.clipboard.writeText(text); if (!signal.aborted) opened.value = false; return;
    }
    if (action === 'copyPatch') {
      const result = await linesApi.prepare({ file: { repositoryId: props.repositoryId, workspaceId: props.workspaceId, path: props.context.path, area: props.context.area }, lines: props.selection!.ids, token: props.snapshotToken!, exportPatch: true }, signal);
      if (signal.aborted) return; if (!result.result?.patch) throw new Error(); await navigator.clipboard.writeText(result.result.patch); if (!signal.aborted) opened.value = false; return;
    }
    if (action === 'discard' || action === 'stash' || action === 'ignore') {
      const value = { ...request(action, props.context.paths), pattern: option || 'file' }; const response = await api.prepare(value, signal);
      if (signal.aborted) return; if (!response.result) throw new Error(); reviewed = value; plan.value = response.result; dialog.value = 'review'; opened.value = false; return;
    }
    if (['history', 'lineHistory', 'blame', 'patch'].includes(action)) {
      const response = await api.read(request(action, action === 'patch' ? props.context.paths : undefined), signal);
      if (signal.aborted) return; if (!response.result) throw new Error(); view.value = response.result; opened.value = false;
      if (action === 'patch') download(response.result.patch); else dialog.value = action;
      return;
    }
    const response = await api.desktop({ ...request(action), tool: option }, signal); if (signal.aborted) return; if (response.result !== true) throw new Error(); opened.value = false;
  });
}
async function confirm() {
  if (!plan.value || !reviewed) return;
  await task(async () => {
    try {
      const result = await api.execute({ ...reviewed!, token: plan.value!.token }, controller.signal);
      if (controller.signal.aborted) return; if (result.result !== true) throw new Error(); plan.value = null; dialog.value = ''; emit('changed');
    } catch (cause) { if (!controller.signal.aborted) { uncertain.value = true; plan.value = null; emit('uncertain'); } throw cause; }
  });
}
function selectCommit(sha: string) { preferences.repositoryTabs[props.repositoryId] = 'history'; preferences.historyNavigation[props.repositoryId] = { branch: '', sha, revision: (preferences.historyNavigation[props.repositoryId]?.revision ?? 0) + 1 }; close(); }
function outside(event: PointerEvent) { if (opened.value && (!(event.target instanceof Node) || !popup.value?.contains(event.target))) close(); }
function keyboard(event: KeyboardEvent) {
  if (event.key === 'Escape') { event.preventDefault(); close(); return; }
  if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
  const buttons = [...popup.value!.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')].filter(button => !button.closest('details') || button.closest('details')!.open);
  const index = buttons.indexOf(document.activeElement as HTMLButtonElement); const next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : (index + (event.key === 'ArrowUp' ? -1 : 1) + buttons.length) % buttons.length;
  event.preventDefault(); buttons[next]?.focus();
}
onMounted(async () => { window.addEventListener('pointerdown', outside); await task(async () => { const response = await api.read(request('info'), controller.signal); if (!controller.signal.aborted) { if (!response.result) throw new Error(); view.value = response.result; } }); await nextTick(); popup.value?.querySelector<HTMLButtonElement>('button')?.focus(); });
watch([() => props.repositoryId, () => props.workspaceId, () => props.context.path, () => props.context.area, () => i18n.language], () => { controller.abort(); opened.value = false; dialog.value = ''; plan.value = null; reviewed = undefined; emit('busy', false); emit('close'); });
onUnmounted(() => { controller.abort(); window.removeEventListener('pointerdown', outside); emit('busy', false); });
</script>
<template>
  <Teleport to="body"><div v-if="opened" ref="popup" class="local-file-popup" role="menu" :style="position" @keydown="keyboard">
    <template v-if="selection">
      <details><summary>{{ i18n.t('localRevealLine') }}</summary><button v-for="editor in view?.editors" :key="editor.id" role="menuitem" :disabled="busy" @click="run('line', editor.id)">{{ editor.name }}</button><small v-if="!view?.editors.length">{{ i18n.t('commitNoTools') }}</small></details>
      <button role="menuitem" :disabled="busy" @click="run('lineHistory')">{{ i18n.t('localLinesHistory') }}</button>
      <button role="menuitem" :disabled="busy" @click="run('copySelection')">{{ i18n.t('localCopy') }}</button>
      <button role="menuitem" :disabled="busy || !selection.ids.length || !snapshotToken" @click="run('copyPatch')">{{ i18n.t('localCopyPatch') }}</button>
    </template>
    <template v-else>
      <button role="menuitem" :disabled="busy" @click="run('open')">{{ i18n.t('commitOpen') }}</button>
      <details><summary>{{ i18n.t('commitExternalDiff') }}</summary><button v-for="tool in view?.tools" :key="tool.id" role="menuitem" :disabled="busy" @click="run('diff', tool.id)">{{ tool.name }}</button><small v-if="!view?.tools.length">{{ i18n.t('commitNoTools') }}</small></details>
      <button role="menuitem" :disabled="busy" @click="run('reveal')">{{ i18n.t('commitReveal') }}</button>
      <button role="menuitem" :disabled="busy" @click="run('blame')">{{ i18n.t('commitBlame') }}</button>
      <button role="menuitem" :disabled="busy" @click="run('history')">{{ i18n.t('commitFileHistory') }}</button>
      <hr /><button role="menuitem" :disabled="busy" @click="run('move')">{{ i18n.t(context.area === 'staged' ? 'editUnstage' : 'editStage') }} ({{ context.paths.length }})</button>
      <button v-if="context.area === 'working'" role="menuitem" :disabled="busy" @click="run('discard')">{{ i18n.t('localDiscard') }}…</button>
      <button role="menuitem" :disabled="busy" @click="run('all')">{{ i18n.t(context.area === 'staged' ? 'localUnstageAll' : 'localStageAll') }}</button>
      <details v-if="context.area === 'working'"><summary>{{ i18n.t('localIgnore') }}</summary><button v-for="pattern in (['file', 'extension', 'folder'] as const)" :key="pattern" role="menuitem" :disabled="busy || !untracked" @click="run('ignore', pattern)">{{ i18n.t(pattern === 'file' ? 'localIgnoreFile' : pattern === 'extension' ? 'localIgnoreExtension' : 'localIgnoreFolder') }}</button></details>
      <button role="menuitem" :disabled="busy" @click="run('stash')">Stash {{ context.paths.length }} {{ i18n.t('editFiles') }}…</button>
      <button role="menuitem" :disabled="busy" @click="run('patch')">{{ i18n.t('localSavePatch') }}</button><hr />
      <button role="menuitem" :disabled="busy" @click="run('copy')">{{ i18n.t('commitCopyPath') }}</button><button role="menuitem" :disabled="busy || !view" @click="run('full')">{{ i18n.t('commitCopyFullPath') }}</button>
    </template><p v-if="error" role="alert">{{ error }}</p>
  </div></Teleport>
  <p v-if="error && !opened && !dialog" role="alert">{{ error }}</p>
  <Dialog v-if="dialog" visible modal :closable="!busy" :close-on-escape="!busy" :header="i18n.t(dialog === 'review' ? 'localReview' : dialog === 'blame' ? 'commitBlame' : dialog === 'lineHistory' ? 'localLinesHistory' : 'commitFileHistory')" class="workspace-action-dialog" @update:visible="close">
    <template v-if="dialog === 'review'"><template v-if="plan"><p>{{ i18n.t(plan.action === 'discard' ? 'localDiscardWarning' : plan.action === 'stash' ? 'localStashWarning' : 'localIgnoreWarning') }}</p><pre>{{ plan.preview }}</pre><button class="local-confirm" :disabled="busy || uncertain" @click="confirm">{{ i18n.t('editConfirm') }}</button></template></template>
    <table v-else-if="dialog === 'blame'" class="local-preview"><tbody><tr v-for="line in view?.blame.slice(0, 4000)" :key="line.line"><td>{{ line.line }}</td><td>{{ line.sha.slice(0, 8) }}</td><td>{{ line.author }}</td><td>{{ formatter.format(new Date(Number(line.time) * 1000)) }}</td><td><code>{{ line.text }}</code></td></tr></tbody></table>
    <template v-else><ul class="local-preview"><li v-for="commit in view?.history.slice(0, 100)" :key="commit.sha"><button @click="selectCommit(commit.sha)"><code>{{ commit.sha.slice(0, 8) }}</code> {{ commit.subject }} · {{ commit.author }}</button></li></ul><p v-if="!view?.history.length">{{ i18n.t('localNoHistory') }}</p></template>
    <p v-if="(view?.history.length ?? 0) > 100">{{ i18n.t('commitHistoryLimit') }}</p><p v-if="(view?.blame.length ?? 0) > 4000">{{ i18n.t('commitPreviewLimit') }}</p><p v-if="uncertain" role="alert">{{ i18n.t('linesUncertain') }}</p><p v-if="error" role="alert">{{ error }}</p><button :disabled="busy" @click="close">{{ i18n.t('editCancel') }}</button>
    <p v-if="dialog === 'blame' && !view?.blame.length">{{ i18n.t('localNoHistory') }}</p>
  </Dialog>
</template>
<style scoped>
.local-file-popup { position: fixed; z-index: 1250; width: min(270px, calc(100vw - 16px)); max-height: calc(100dvh - 16px); overflow: auto; background: var(--gc-surface-raised); border: 1px solid var(--gc-border); border-radius: .4rem; padding: .3rem; }
.local-file-popup button, .local-file-popup summary { display: block; width: 100%; border: 0; padding: .4rem .6rem; background: transparent; color: var(--gc-text); text-align: left; cursor: pointer; }
.local-file-popup button:hover, .local-file-popup summary:hover { background: var(--gc-selection-bg); }
.local-file-popup details button { padding-left: 1.5rem; }
.local-preview, pre { display: block; max-height: 60vh; max-width: 80vw; overflow: auto; }
.local-preview td { padding: .2rem; vertical-align: top; }
.local-preview code { white-space: pre; }
[role='alert'] { color: var(--gc-danger); }
hr { border: 0; border-top: 1px solid var(--gc-divider); }
</style>
