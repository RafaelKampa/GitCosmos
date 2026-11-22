<script setup lang="ts">
import { onUnmounted, ref } from 'vue';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import { LocalFolderApiService } from '../../../core/file-system/local-folder-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';

const props = defineProps<{ modelValue: string; id: string; label: string; help: string; disabled: boolean }>();
const emit = defineEmits<{ 'update:modelValue': [value: string]; busy: [value: boolean]; error: [message: string] }>();
const i18n = useLanguageStore();
const api = new LocalFolderApiService(() => i18n.language, i18n.t);
const selecting = ref(false);
let controller: AbortController | undefined;
function update(value: string | undefined) { emit('update:modelValue', value ?? ''); }
async function selectFolder() {
  if (selecting.value || props.disabled) return;
  const request = new AbortController();
  controller = request;
  selecting.value = true;
  emit('busy', true);
  emit('error', '');
  try {
    const response = await api.selectFolder({ initialPath: props.modelValue.trim() }, request.signal);
    if (!request.signal.aborted) {
      if (!response.result) throw new ApiError(i18n.t('folderSelectionError'));
      if (!response.result.cancelled) {
        if (!response.result.localPath) throw new ApiError(i18n.t('folderSelectionError'));
        emit('update:modelValue', response.result.localPath);
      }
    }
  } catch (cause: unknown) {
    if (!request.signal.aborted)
      emit('error', cause instanceof ApiError ? cause.message : i18n.t('folderSelectionError'));
  } finally {
    if (!request.signal.aborted) { selecting.value = false; emit('busy', false); }
  }
}
onUnmounted(() => controller?.abort());
</script>

<template>
  <div class="folder-path-field">
    <label :for="id">{{ label }}</label>
    <div class="folder-path-controls">
      <InputText :id="id" :model-value="modelValue" required maxlength="32767" autocomplete="off"
        :disabled="disabled || selecting" :aria-describedby="id + '-help'" @update:model-value="update" />
      <Button type="button" class="repository-folder-picker" severity="secondary"
        :label="i18n.t(selecting ? 'selectingFolder' : 'selectFolder')" :disabled="disabled || selecting" @click="selectFolder" />
    </div>
    <small :id="id + '-help'">{{ help }}</small>
  </div>
</template>

<style scoped>
.folder-path-field { display: grid; gap: .75rem; }
.folder-path-controls { display: flex; flex-wrap: wrap; gap: .75rem; }
.folder-path-controls input { flex: 1; min-width: min(100%, 15rem); width: auto; }
.repository-folder-picker { flex-shrink: 0; }
small { color: var(--gc-text-muted); }
</style>
