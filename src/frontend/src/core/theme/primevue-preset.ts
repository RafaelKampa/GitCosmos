import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

// All custom colors come from shared/styles/tokens.css, including PrimeVue colors.
const colorScheme = {
  surface: {
    0: 'var(--color-neutral-0)', 50: 'var(--color-neutral-50)',
    100: 'var(--color-neutral-100)', 200: 'var(--color-neutral-200)',
    300: 'var(--color-neutral-300)', 400: 'var(--color-neutral-400)',
    500: 'var(--color-neutral-500)', 600: 'var(--color-neutral-600)',
    700: 'var(--color-neutral-700)', 800: 'var(--color-neutral-800)',
    900: 'var(--color-neutral-900)', 950: 'var(--color-neutral-950)',
  },
  primary: {
    color: 'var(--color-action)', inverseColor: 'var(--color-on-action)',
    hoverColor: 'var(--color-action-hover)', activeColor: 'var(--color-action-active)',
  },
  highlight: {
    background: 'var(--color-selection)', focusBackground: 'var(--color-selection-focus)',
    color: 'var(--color-selection-text)', focusColor: 'var(--color-selection-text)',
  },
  text: {
    color: 'var(--color-text)', hoverColor: 'var(--color-text)',
    mutedColor: 'var(--color-text-muted)', hoverMutedColor: 'var(--color-text)',
  },
  content: {
    background: 'var(--color-surface)', hoverBackground: 'var(--color-surface-hover)',
    borderColor: 'var(--color-border)', color: 'var(--color-text)', hoverColor: 'var(--color-text)',
  },
};

export const gitCosmosPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: 'var(--color-primary-50)', 100: 'var(--color-primary-100)',
      200: 'var(--color-primary-200)', 300: 'var(--color-primary-300)',
      400: 'var(--color-primary-400)', 500: 'var(--color-primary-500)',
      600: 'var(--color-primary-600)', 700: 'var(--color-primary-700)',
      800: 'var(--color-primary-800)', 900: 'var(--color-primary-900)',
      950: 'var(--color-primary-950)',
    },
    focusRing: { color: 'var(--color-focus-ring)' },
    colorScheme: { light: colorScheme, dark: colorScheme },
  },
});
