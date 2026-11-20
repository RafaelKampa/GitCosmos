import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import ThemeSwitcher from './ThemeSwitcher.vue';
import { useLanguageStore } from '../../../core/i18n/language.store';

describe('theme switcher', () => {
  let wrapper: VueWrapper | undefined;
  beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
  afterEach(() => { wrapper?.unmount(); document.documentElement.classList.remove('dark'); });

  it.each([
    ['pt-BR', 'Claro', 'Escuro', 'Alternar tema escuro', 'Ativar tema escuro', 'Ativar tema claro'],
    ['es', 'Claro', 'Oscuro', 'Alternar tema oscuro', 'Activar tema oscuro', 'Activar tema claro'],
    ['en', 'Light', 'Dark', 'Toggle dark theme', 'Switch to dark theme', 'Switch to light theme'],
  ])('provides an accessible control in %s', async (language, light, dark, label, activateDark, activateLight) => {
    localStorage.setItem('gitcosmos.theme', 'light');
    useLanguageStore().setLanguage(language);
    wrapper = mount(ThemeSwitcher);
    const button = wrapper.get('button');
    expect(button.attributes('aria-label')).toBe(label);
    expect(button.attributes('aria-pressed')).toBe('false');
    expect(button.attributes('title')).toBe(activateDark);
    expect(button.text()).toContain(light);
    await button.trigger('click');
    expect(button.attributes('aria-pressed')).toBe('true');
    expect(button.attributes('title')).toBe(activateLight);
    expect(button.text()).toContain(dark);
    expect(document.documentElement.dataset.theme).toBe('cosmic-dark');
    await button.trigger('click');
    expect(button.attributes('aria-pressed')).toBe('false');
    expect(localStorage.getItem('gitcosmos.theme')).toBe('light');
  });

  it('starts with dark mode selected when no preference has been saved', () => {
    wrapper = mount(ThemeSwitcher);
    expect(wrapper.get('button').attributes('aria-pressed')).toBe('true');
    expect(wrapper.get('button').text()).toContain('Escuro');
  });
});
