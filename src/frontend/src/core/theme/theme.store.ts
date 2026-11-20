import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

export type Theme = 'light' | 'dark';
const storageKey = 'gitcosmos.theme';

export const useThemeStore = defineStore('theme', () => {
  const theme = ref<Theme>(localStorage.getItem(storageKey) === 'light' ? 'light' : 'dark');
  const isDark = computed(() => theme.value === 'dark');

  function initialize(): void {
    document.documentElement.dataset.theme = `cosmic-${theme.value}`;
    document.documentElement.classList.toggle('dark', isDark.value);
  }

  function setTheme(value: Theme): void {
    theme.value = value;
    localStorage.setItem(storageKey, value);
    initialize();
  }

  function toggle(): void { setTheme(isDark.value ? 'light' : 'dark'); }

  return { theme, isDark, initialize, setTheme, toggle };
});
