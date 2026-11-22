<script setup lang="ts">
import { ref } from 'vue';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Button from 'primevue/button';
import type { WorkspaceResponse } from '../../../core/workspaces/models/responses/workspace-response.model';
import { useLanguageStore } from '../../../core/i18n/language.store';

const props = defineProps<{ workspace: WorkspaceResponse; mode: 'rename' | 'delete'; busy: boolean; error: string }>();
const emit = defineEmits<{ rename: [name: string]; delete: []; cancel: [] }>();
const i18n = useLanguageStore();
const name = ref(props.workspace.name);
</script>

<template>
  <Dialog :visible="true" modal :closable="!busy" :close-on-escape="!busy" :dismissable-mask="false"
    :header="i18n.t(mode === 'rename' ? 'renameWorkspace' : 'deleteWorkspace')"
    class="workspace-action-dialog" :style="{ width: '32rem', maxWidth: 'calc(100vw - 2rem)' }"
    @update:visible="emit('cancel')">
    <form :aria-busy="busy" @submit.prevent="mode === 'rename' ? emit('rename', name.trim()) : emit('delete')">
      <template v-if="mode === 'rename'">
        <label for="rename-workspace-name">{{ i18n.t('workspaceName') }}</label>
        <InputText id="rename-workspace-name" v-model="name" required maxlength="100" :disabled="busy"
          autofocus autocomplete="off" aria-describedby="rename-workspace-help" />
        <small id="rename-workspace-help">{{ i18n.t('workspaceNameHelp') }}</small>
      </template>
      <template v-else>
        <p>{{ i18n.t('workspaceDeleteConfirmation') }}</p>
        <p class="workspace-delete-name"><strong>{{ workspace.name }}</strong></p>
        <p>{{ i18n.t('workspaceDeleteHint') }}</p>
      </template>
      <p v-if="error" role="alert">{{ error }}</p>
      <div class="workspace-dialog-actions">
        <Button type="button" :label="i18n.t('cancel')" severity="secondary" :disabled="busy"
          :autofocus="mode === 'delete'" @click="emit('cancel')" />
        <Button v-if="mode === 'rename'" type="submit" :label="i18n.t('saveWorkspace')" :disabled="busy" />
        <button v-else type="submit" class="workspace-danger-action" :disabled="busy">{{ i18n.t('deleteWorkspace') }}</button>
      </div>
    </form>
  </Dialog>
</template>
