<script setup lang="ts">
import { RouterLink } from 'vue-router';
import LanguageSwitcher from '../../shared/components/language-switcher/LanguageSwitcher.vue';
import ThemeSwitcher from '../../shared/components/theme-switcher/ThemeSwitcher.vue';
import { useLanguageStore } from '../../core/i18n/language.store';
import { useExplorerPreferences } from '../../core/preferences/explorer-preferences.store';
import logo from '../../shared/assets/branding/Logo_GitCosmos.png';
const i18n = useLanguageStore(); const preferences = useExplorerPreferences();
</script>
<template>
<header class="app-header">
<div class="app-brand">
<button type="button" class="icon-action explorer-toggle" :aria-label="i18n.t('toggleExplorer')" :aria-expanded="!preferences.collapsed" aria-controls="explorer-sidebar" @click="preferences.collapsed = !preferences.collapsed">☰</button>
<RouterLink to="/" :aria-label="i18n.t('missionControl')">
<img :src="logo" class="brand-logo" alt="GitCosmos" width="2172" height="724" />
</RouterLink>
</div>
<div class="header-controls">
<LanguageSwitcher />
<ThemeSwitcher />
<RouterLink class="icon-action settings-link" to="/settings" :aria-label="i18n.t('settings')" :title="i18n.t('settings')">⚙</RouterLink>
</div>
</header>
</template>
