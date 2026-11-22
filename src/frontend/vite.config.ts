import { defineConfig } from 'vitest/config';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: { '/api': { target: 'http://127.0.0.1:5210', changeOrigin: true } },
  },
  test: {
    environment: 'jsdom',
    maxWorkers: 1,
    testTimeout: 15000,
    restoreMocks: true,
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,vue}'],
      // Static translation catalogs contain no measured executable items (0/0).
      // Their behavior remains exercised by the language and component tests.
      exclude: ['src/**/*.spec.ts', 'src/**/*.model.ts', 'src/core/i18n/translations/**'],
      reporter: ['text', 'json-summary', 'html'],
      thresholds: { perFile: true, lines: 95, branches: 95, functions: 95, statements: 95 },
    },
  },
});
