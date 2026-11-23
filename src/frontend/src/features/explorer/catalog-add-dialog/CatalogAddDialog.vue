<script setup lang="ts">
import { onUnmounted, ref } from 'vue';
import Dialog from 'primevue/dialog';
import Button from 'primevue/button';
import FolderPathField from '../../../shared/components/folder-path-field/FolderPathField.vue';
import RepositoryCloneForm from '../../workspaces/repository-clone-form/RepositoryCloneForm.vue';
import { CatalogApiService } from '../../../core/catalog/catalog-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';
import type { RepositoryImportResponse } from '../../../core/workspaces/models/responses/repository-import-response.model';
const emit = defineEmits<{ close: []; changed: [] }>();
const i18n = useLanguageStore(); const api = new CatalogApiService(() => i18n.language, i18n.t);
const mode = ref<'local' | 'clone'>('local'); const path = ref(''); const busy = ref(false); const selecting = ref(false);
const error = ref(''); const result = ref<RepositoryImportResponse | null>(null); const message = ref('');
let request: AbortController | undefined;
async function add() {
  if (busy.value || selecting.value) return;
  const current = new AbortController(); request = current; busy.value = true; error.value = ''; result.value = null; message.value = '';
  try { const response = await api.addRepository(path.value.trim(), current.signal); if (!current.signal.aborted) { if (!response.result) throw new ApiError(i18n.t('repositoryError')); result.value = response.result; message.value = response.messages.join(' '); emit('changed'); } }
  catch (cause: unknown) { if (!current.signal.aborted) error.value = cause instanceof ApiError ? cause.message : i18n.t('repositoryError'); }
  finally { if (!current.signal.aborted) busy.value = false; }
}
function close() { if (!busy.value && !selecting.value) emit('close'); }
onUnmounted(() => request?.abort());
</script>
<template>
<Dialog :visible="true" modal :closable="!busy && !selecting" :close-on-escape="!busy && !selecting" :dismissable-mask="false" :header="i18n.t('addRepository')" class="workspace-action-dialog" :style="{ width: '46rem', maxWidth: 'calc(100vw - 2rem)' }" @update:visible="close">
  <div class="view-toggle add-mode">
<button type="button" :disabled="busy || selecting" :aria-pressed="mode === 'local'" @click="mode = 'local'">{{ i18n.t('addLocalRepository') }}</button>
<button type="button" :disabled="busy || selecting" :aria-pressed="mode === 'clone'" @click="mode = 'clone'">{{ i18n.t('cloneRepository') }}</button>
</div>
  <form v-if="mode === 'local'" @submit.prevent="add">
<FolderPathField id="catalog-path" v-model="path" :disabled="busy" :label="i18n.t('repositoryPath')" :help="i18n.t('importHelp')" @busy="selecting = $event" @error="error = $event" />
<Button type="submit" :label="i18n.t('addRepository')" :disabled="busy || selecting" />
</form>
<RepositoryCloneForm v-else :workspace-id="0" :disabled="selecting" @busy="busy = $event" @changed="emit('changed')" />
  <p v-if="error" role="alert">{{ error }}</p>
<p v-if="message" role="status">{{ message }}</p>
<ul v-if="result" class="import-outcomes">
<li v-for="item in result.items" :key="item.localPath">
<code>{{ item.localPath }}</code> — {{ item.message }}</li>
</ul>
<div class="workspace-dialog-actions">
<Button :label="i18n.t('closeRepositories')" severity="secondary" :disabled="busy || selecting" @click="close" />
</div>
</Dialog>
</template>
