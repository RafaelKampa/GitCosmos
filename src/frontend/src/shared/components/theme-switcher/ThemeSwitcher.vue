<script setup lang="ts">
import MaterialIcon from '../material-icon/MaterialIcon.vue';
import { useThemeStore } from '../../../core/theme/theme.store';
import { useLanguageStore } from '../../../core/i18n/language.store';

const theme = useThemeStore();
const i18n = useLanguageStore();
</script>

<template>
  <button
    type="button"
    class="theme-switcher"
    :aria-label="i18n.t('toggleDarkTheme')"
    :aria-pressed="theme.isDark"
    :title="i18n.t(theme.isDark ? 'switchToLight' : 'switchToDark')"
    @click="theme.toggle"
  >
    <MaterialIcon :name="theme.isDark ? 'dark_mode' : 'light_mode'" />
    <span>{{ i18n.t(theme.isDark ? 'darkTheme' : 'lightTheme') }}</span>
  </button>
</template>
