<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue';
import Dialog from 'primevue/dialog';
import MaterialIcon from '../../../shared/components/material-icon/MaterialIcon.vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { useExplorerPreferences } from '../../../core/preferences/explorer-preferences.store';
import { ApiError } from '../../../core/api/api-error';
import { CommitExplorerApiService } from '../../../core/repositories/commit-explorer-api.service';
import type { CommitExternalTool, CommitBlameLine, CommitRestorePlan } from '../../../core/repositories/models/commit-explorer.model';
import type { GitCommit } from '../../../core/repositories/models/repository-history.model';
const props = withDefaults(defineProps<{ repositoryId: number; workspaceId: number; sha: string; path: string; parent?: string; deleted?: boolean; changed?: boolean }>(), { parent: '', deleted: false, changed: true });
const emit = defineEmits<{ showTree: [path: string] }>();
const i18n = useLanguageStore(); const preferences = useExplorerPreferences(); const api = new CommitExplorerApiService(() => i18n.language, i18n.t);
const file = computed(() => ({ repositoryId: props.repositoryId, workspaceId: props.workspaceId, sha: props.sha, path: props.path }));
const readable = computed(() => ({ ...file.value, sha: props.deleted ? props.parent : props.sha }));
const opened = ref(false); const root = ref<HTMLElement>(); const popup = ref<HTMLElement>(); const trigger = ref<HTMLButtonElement>(); const position = ref({ top: '0px', left: '0px' });
const tools = ref<CommitExternalTool[]>([]); const busy = ref(false); const error = ref(''); const notice = ref('');
const dialog = ref(''); const plan = ref<CommitRestorePlan | null>(null); const before = ref(false); const commits = ref<GitCommit[]>([]); const blame = ref<CommitBlameLine[]>([]);
const blameDateFormatter = computed(() => new Intl.DateTimeFormat(i18n.language, { year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric', second: 'numeric' }));
let controller: AbortController | undefined;
function close() { opened.value = false; trigger.value?.focus(); }
function outside(event: PointerEvent) { if (!root.value?.contains(event.target as Node) && !popup.value?.contains(event.target as Node)) close(); }
async function task(action: (signal: AbortSignal) => Promise<void>) {
  if (busy.value) return;
  controller?.abort(); controller = new AbortController(); const signal = controller.signal;
  busy.value = true; error.value = ''; notice.value = '';
  try { await action(signal); } catch (cause) { if (!signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('commitFileError'); }
  finally { if (!signal.aborted) busy.value = false; }
}
async function toggle() {
  if (opened.value) { close(); return; }
  const rect = trigger.value!.getBoundingClientRect();
  position.value = { top: Math.max(8, Math.min(rect.bottom, window.innerHeight - 390)) + 'px', left: Math.max(8, Math.min(rect.left, window.innerWidth - 260)) + 'px' };
  opened.value = true;
  window.addEventListener('pointerdown', outside);
  await nextTick(); popup.value?.querySelector<HTMLButtonElement>('button')?.focus();
  if (props.changed) await task(async signal => { const result = await api.tools(file.value, signal); if (!signal.aborted) tools.value = result.result ?? []; });
}
function keyboard(event: KeyboardEvent) {
  if (event.key === 'Escape') { close(); return; }
  if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
  event.preventDefault(); const buttons = [...popup.value!.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')].filter(button => !button.closest('details') || button.closest('details')!.open); const current = buttons.indexOf(document.activeElement as HTMLButtonElement);
  const index = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : (current + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length;
  buttons[index]?.focus();
}
async function run(action: string, tool = '') {
  close();
  if (action === 'tree') { emit('showTree', props.path); return; }
  await task(async signal => {
    if (action === 'copy') { await navigator.clipboard.writeText(props.path); if (!signal.aborted) notice.value = i18n.t('commitActionDone'); return; }
    if (action === 'full' || action === 'save') {
      const result = await api.file(readable.value, signal); if (signal.aborted) return; if (!result.result) throw new Error();
      if (action === 'full') await navigator.clipboard.writeText(result.result.fullPath);
      else {
        if (result.result.kind === 'submodule') throw new ApiError(i18n.t('diffSubmodule'));
        const data = Uint8Array.from(atob(result.result.base64), ch => ch.charCodeAt(0));
        const url = URL.createObjectURL(new Blob([data])); const link = document.createElement('a'); link.href = url; link.download = props.path.split('/').at(-1)!; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
      }
      if (!signal.aborted) notice.value = i18n.t('commitActionDone'); return;
    }
    if (action === 'history') { const response = await api.history(file.value, signal); if (!signal.aborted) { commits.value = response.result ?? []; dialog.value = 'history'; } return; }
    if (action === 'blame') { const response = await api.blame(readable.value, signal); if (!signal.aborted) { blame.value = response.result ?? []; dialog.value = 'blame'; } return; }
    if (action === 'restore' || action === 'before') {
      const value = action === 'before'; const response = await api.prepare(file.value, value, signal);
      if (!signal.aborted) { if (!response.result) throw new Error(); before.value = value; plan.value = response.result; dialog.value = 'restore'; } return;
    }
    const response = await api.desktop(action === 'open' ? readable.value : file.value, action, tool, signal); if (!signal.aborted) { if (response.result !== true) throw new Error(); notice.value = i18n.t('commitActionDone'); }
  });
}
async function restore() {
  await task(async signal => {
    const response = await api.restore(file.value, before.value, plan.value!.token, signal);
    if (!signal.aborted) { if (response.result !== true) throw new Error(); dialog.value = ''; plan.value = null; preferences.repositoryVersions[props.repositoryId] = (preferences.repositoryVersions[props.repositoryId] || 0) + 1; notice.value = i18n.t('commitActionDone'); }
  });
}
function selectCommit(sha: string) { preferences.historyNavigation[props.repositoryId] = { branch: '', sha, revision: (preferences.historyNavigation[props.repositoryId]?.revision || 0) + 1 }; dialog.value = ''; }
watch(opened, value => { if (!value) window.removeEventListener('pointerdown', outside); });
watch([() => props.sha, () => props.path, () => i18n.language], () => { controller?.abort(); busy.value = false; opened.value = false; dialog.value = ''; error.value = ''; notice.value = ''; });
onUnmounted(() => { controller?.abort(); window.removeEventListener('pointerdown', outside); });
</script>
<template>
  <span ref="root" class="commit-file-menu">
    <button ref="trigger" class="file-options" type="button" :aria-label="i18n.t('commitFileOptions') + ': ' + path" aria-haspopup="menu" :aria-expanded="opened" :disabled="busy" @click.stop="toggle"><MaterialIcon name="more_horiz" /></button>
    <Teleport to="body"><div v-if="opened" ref="popup" class="commit-file-popup" role="menu" :style="position" @keydown="keyboard">
      <button role="menuitem" :disabled="busy || deleted && !parent" @click="run('open')">{{ i18n.t('commitOpen') }}</button>
      <details v-if="changed"><summary>{{ i18n.t('commitExternalDiff') }}</summary><button v-for="tool in tools" :key="tool.id" role="menuitem" :disabled="busy" @click="run('diff', tool.id)">{{ tool.name }}</button><small v-if="!tools.length">{{ i18n.t('commitNoTools') }}</small></details>
      <button role="menuitem" :disabled="busy" @click="run('reveal')">{{ i18n.t('commitReveal') }}</button>
      <details v-if="changed"><summary>{{ i18n.t('commitReset') }}</summary><button role="menuitem" :disabled="busy" @click="run('restore')">{{ i18n.t('commitStateAt') }}</button><button role="menuitem" :disabled="busy" @click="run('before')">{{ i18n.t('commitStateBefore') }}</button></details>
      <button role="menuitem" :disabled="busy || deleted && !parent" @click="run('blame')">{{ i18n.t('commitBlame') }}</button>
      <button role="menuitem" :disabled="busy" @click="run('history')">{{ i18n.t('commitFileHistory') }}</button>
      <button v-if="changed" role="menuitem" :disabled="deleted" @click="run('tree')">{{ i18n.t('commitShowTree') }}</button>
      <button role="menuitem" :disabled="busy || deleted && !parent" @click="run('save')">{{ i18n.t('commitSave') }}</button>
      <button role="menuitem" :disabled="busy" @click="run('copy')">{{ i18n.t('commitCopyPath') }}</button>
      <button role="menuitem" :disabled="busy || deleted && !parent" @click="run('full')">{{ i18n.t('commitCopyFullPath') }}</button>
    </div></Teleport>
    <span v-if="error" role="alert" class="file-action-feedback">{{ error }}</span><span v-if="notice" role="status" class="file-action-feedback">{{ notice }}</span>
  </span>
  <Dialog v-if="dialog" visible modal :header="i18n.t(dialog === 'restore' ? 'commitReset' : dialog === 'blame' ? 'commitBlame' : 'commitFileHistory')" :closable="!busy" :close-on-escape="!busy" class="workspace-action-dialog commit-file-dialog" @update:visible="dialog = ''">
    <p><code>{{ path }}</code></p>
    <template v-if="dialog === 'restore' && plan"><p>{{ i18n.t('commitRestoreWarning') }}</p><p>{{ i18n.t(before ? 'commitStateBefore' : 'commitStateAt') }}: <code>{{ plan.sourceSha || '∅' }}</code></p><p v-if="!plan.exists">{{ i18n.t('commitWillRemove') }}</p><button class="confirm-file-restore" :disabled="busy" @click="restore">{{ i18n.t('editConfirm') }}</button></template>
    <template v-else-if="dialog === 'history'"><p v-if="commits.length > 100">{{ i18n.t('commitHistoryLimit') }}</p><ul class="file-history"><li v-for="commit in commits.slice(0, 100)" :key="commit.sha"><button @click="selectCommit(commit.sha)"><code>{{ commit.sha.slice(0, 8) }}</code> {{ commit.subject }} · {{ commit.author }}</button></li></ul></template>
    <table v-else-if="dialog === 'blame'" class="blame-table"><tbody><tr v-for="line in blame.slice(0, 4000)" :key="line.line"><td>{{ line.line }}</td><td><button :title="line.sha" @click="selectCommit(line.sha)">{{ line.sha.slice(0, 8) }}</button></td><td>{{ line.author }}</td><td><time>{{ blameDateFormatter.format(new Date(Number(line.time) * 1000)) }}</time></td><td><code>{{ line.text }}</code></td></tr></tbody></table>
    <p v-if="dialog === 'blame' && blame.length > 4000">{{ i18n.t('commitPreviewLimit') }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
  </Dialog>
</template>
<style scoped>
.commit-file-menu { flex-shrink: 0; display: inline-flex; }
.file-options { border: 0; padding: .2rem; color: var(--gc-text-muted); background: transparent; }
.commit-file-popup { position: fixed; z-index: 1250; width: min(250px, calc(100vw - 16px)); max-height: calc(100dvh - 16px); overflow: auto; background: var(--gc-surface-raised); border: 1px solid var(--gc-border); border-radius: .4rem; padding: .3rem; }
.commit-file-popup button, .commit-file-popup summary { display: block; width: 100%; border: 0; padding: .45rem .6rem; background: transparent; color: var(--gc-text); text-align: left; cursor: pointer; }
.commit-file-popup button:hover, .commit-file-popup summary:hover { background: var(--gc-selection-bg); }
.commit-file-popup details button { padding-left: 1.5rem; }
.file-action-feedback { font-size: .75rem; max-width: 20rem; overflow-wrap: anywhere; }
.file-history { padding-left: 1rem; }
.blame-table { display: block; max-width: 80vw; max-height: 65vh; overflow: auto; }
.blame-table td { padding: .25rem; vertical-align: top; }
.blame-table code { white-space: pre; }
</style>
