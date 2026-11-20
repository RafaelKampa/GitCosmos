import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import pt from './translations/pt-BR/app';
import es from './translations/es/app';
import en from './translations/en/app';

export type Language = 'pt-BR' | 'es' | 'en';
export type TranslationKey = keyof typeof pt;
const catalogs = { 'pt-BR': pt, es, en };

export function normalizeLanguage(value: string | null): Language {
  const language = (value ?? '').toLowerCase();
  if (language.startsWith('es')) return 'es';
  if (language.startsWith('en')) return 'en';
  return 'pt-BR';
}

export const useLanguageStore = defineStore('language', () => {
  const language = ref<Language>(normalizeLanguage(localStorage.getItem('gitcosmos.language')));
  const messages = computed(() => catalogs[language.value]);
  function t(key: TranslationKey): string { return messages.value[key]; }
  function setLanguage(value: string): void {
    language.value = normalizeLanguage(value);
    localStorage.setItem('gitcosmos.language', language.value);
    document.documentElement.lang = language.value;
  }
  return { language, t, setLanguage };
});
