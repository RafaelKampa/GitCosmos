<script setup lang="ts">
import { useLanguageStore } from '../../../core/i18n/language.store';
const i18n = useLanguageStore();
</script>

<template>
  <label class="language-switcher">
    <span>{{ i18n.t('language') }}</span>
    <select :value="i18n.language" @change="i18n.setLanguage(($event.target as HTMLSelectElement).value)">
      <option value="pt-BR">Português (BR)</option>
      <option value="es">Español</option>
      <option value="en">English</option>
    </select>
  </label>
</template>
