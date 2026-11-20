import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useThemeStore } from './theme.store';

describe('theme preference', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.classList.remove('dark');
    setActivePinia(createPinia());
  });

  it.each([null, 'dark', 'invalid'])('defaults to Cosmic Dark for preference %s', preference => {
    if (preference !== null) localStorage.setItem('gitcosmos.theme', preference);
    const store = useThemeStore();
    document.documentElement.classList.remove('dark');
    store.initialize();
    expect(store.theme).toBe('dark');
    expect(store.isDark).toBe(true);
    expect(document.documentElement.dataset.theme).toBe('cosmic-dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('preserves a saved light preference instead of replacing it with the new default', () => {
    localStorage.setItem('gitcosmos.theme', 'light');
    const store = useThemeStore();
    store.initialize();
    expect(store.isDark).toBe(false);
    expect(document.documentElement.dataset.theme).toBe('cosmic-light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
    expect(localStorage.getItem('gitcosmos.theme')).toBe('light');
  });

  it('toggles both ways and persists the preference across store instances', () => {
    const store = useThemeStore();
    store.toggle();
    expect(localStorage.getItem('gitcosmos.theme')).toBe('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
    setActivePinia(createPinia());
    const restored = useThemeStore();
    expect(restored.theme).toBe('light');
    restored.toggle();
    expect(restored.theme).toBe('dark');
    expect(localStorage.getItem('gitcosmos.theme')).toBe('dark');
    expect(document.documentElement.dataset.theme).toBe('cosmic-dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });
});
