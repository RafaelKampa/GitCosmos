import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { normalizeLanguage, useLanguageStore } from './language.store';
import pt from './translations/pt-BR/app';
import es from './translations/es/app';
import en from './translations/en/app';

describe('languages', () => {
  beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
  it.each([[null, 'pt-BR'], ['', 'pt-BR'], ['fr', 'pt-BR'], ['PT-br', 'pt-BR'], ['ES-py', 'es'], ['EN-us', 'en']])('normalizes %s', (input, expected) => {
    expect(normalizeLanguage(input)).toBe(expected);
  });
  it('defaults to Portuguese and persists the selected language', () => {
    const store = useLanguageStore();
    expect(store.language).toBe('pt-BR');
    expect(store.t('retry')).toBe('Verificar conexão');
    store.setLanguage('es');
    expect(store.t('retry')).toBe('Verificar conexión');
    expect(localStorage.getItem('gitcosmos.language')).toBe('es');
    expect(document.documentElement.lang).toBe('es');
    setActivePinia(createPinia());
    expect(useLanguageStore().language).toBe('es');
  });
  it('provides the same translation keys in all three languages', () => {
    const store = useLanguageStore();
    const values: string[] = [];
    for (const catalog of [pt, es, en]) {
      expect(Object.keys(catalog).sort()).toEqual(Object.keys(pt).sort());
      for (const value of Object.values(catalog)) expect(value.trim().length).toBeGreaterThan(0);
    }
    for (const language of ['pt-BR', 'es', 'en']) {
      store.setLanguage(language);
      values.push(store.t('title'));
      expect(store.t('networkError')).toBeTruthy();
    }
    expect(new Set(values).size).toBe(3);
  });
});
