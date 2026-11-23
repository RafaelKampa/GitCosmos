<script setup lang="ts">
import { RouterLink } from 'vue-router';
import { useLanguageStore } from '../../core/i18n/language.store';
import { useExplorerPreferences } from '../../core/preferences/explorer-preferences.store';
import ThemeSwitcher from '../../shared/components/theme-switcher/ThemeSwitcher.vue';
import LanguageSwitcher from '../../shared/components/language-switcher/LanguageSwitcher.vue';
const i18n = useLanguageStore(); const preferences = useExplorerPreferences();
</script>
<template>
<section class="settings-page">
<h1>{{ i18n.t('settings') }}</h1>
<p class="page-description">{{ i18n.t('settingsDescription') }}</p>
<div class="settings-panel">
<h2>{{ i18n.t('appearance') }}</h2>
<ThemeSwitcher />
<LanguageSwitcher />
<label>{{ i18n.t('explorerWidth') }}<input v-model.number="preferences.sidebarWidth" type="range" min="220" max="380" step="10" />
</label>
<RouterLink to="/connection">{{ i18n.t('statusTitle') }}</RouterLink>
</div>
<div class="settings-panel">
<h2>{{ i18n.t('operations') }}</h2>
<p>{{ i18n.t('operationsUpcoming') }}</p>
</div>
</section>
</template>
