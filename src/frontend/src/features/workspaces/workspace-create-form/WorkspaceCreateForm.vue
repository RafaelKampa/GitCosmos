<script setup lang="ts">
import { ref } from 'vue';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import { useLanguageStore } from '../../../core/i18n/language.store';

defineProps<{ disabled: boolean }>();
const emit = defineEmits<{ create: [name: string] }>();
const i18n = useLanguageStore();
const name = ref('');
</script>

<template>
  <form class="workspace-form" @submit.prevent="emit('create', name.trim())">
    <label for="workspace-name">{{ i18n.t('workspaceName') }}</label>
    <div class="workspace-form-controls">
      <InputText id="workspace-name" v-model="name" required maxlength="100" :disabled="disabled"
        autocomplete="off" aria-describedby="workspace-name-help" />
      <Button type="submit" :label="i18n.t('createWorkspace')" :disabled="disabled" />
    </div>
    <small id="workspace-name-help">{{ i18n.t('workspaceNameHelp') }}</small>
  </form>
</template>

