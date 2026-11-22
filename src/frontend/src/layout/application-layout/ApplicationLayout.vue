<script setup lang="ts">
import LanguageSwitcher from '../../shared/components/language-switcher/LanguageSwitcher.vue';
import ThemeSwitcher from '../../shared/components/theme-switcher/ThemeSwitcher.vue';
import logoPng from '../../shared/assets/branding/Logo_GitCosmos.png';
import { useLanguageStore } from '../../core/i18n/language.store';
const i18n = useLanguageStore();
</script>

<template>
  <header>
    <div>
      <img class="brand-logo" :src="logoPng" alt="GitCosmos" width="2172" height="724" />
      <p>{{ i18n.t('tagline') }}</p>
    </div>
    <div class="header-controls"><ThemeSwitcher /><LanguageSwitcher /></div>
  </header>
  <main>
    <nav class="application-navigation" :aria-label="i18n.t('navigation')">
      <RouterLink to="/">{{ i18n.t('home') }}</RouterLink>
      <RouterLink to="/workspaces">{{ i18n.t('workspaces') }}</RouterLink>
    </nav>
    <RouterView />
  </main>
</template>
