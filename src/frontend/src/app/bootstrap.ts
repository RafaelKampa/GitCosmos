import { createApp } from 'vue';
import { createPinia } from 'pinia';
import PrimeVue from 'primevue/config';
import { gitCosmosPreset } from '../core/theme/primevue-preset';
import { useThemeStore } from '../core/theme/theme.store';
import App from './App.vue';
import { router } from './router';
import { useLanguageStore } from '../core/i18n/language.store';

export function bootstrap(target: string) {
  const app = createApp(App);
  app.use(createPinia());
  app.use(PrimeVue, { theme: { preset: gitCosmosPreset, options: { darkModeSelector: '.dark' } } });
  app.use(router);
  const i18n = useLanguageStore();
  document.documentElement.lang = i18n.language;
  useThemeStore().initialize();
  app.mount(target);
  return app;
}
