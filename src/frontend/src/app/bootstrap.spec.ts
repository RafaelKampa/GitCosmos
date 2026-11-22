import { expect, it, vi } from 'vitest';
import { flushPromises } from '@vue/test-utils';

it('boots the application, loads its route and changes language through the selector', async () => {
  localStorage.clear();
  localStorage.setItem('gitcosmos.language', 'en');
  localStorage.setItem('gitcosmos.theme', 'dark');
  document.body.innerHTML = '<div id="app"></div>';
  vi.stubGlobal('fetch', vi.fn().mockImplementation(async () => new Response(JSON.stringify({
    success: true, result: { name: 'GitCosmos' }, messages: ['Ready'], errors: [],
  }))));
  const { app } = await import('../main');
  const { router } = await import('./router');
  await router.isReady(); await flushPromises();
  expect(document.documentElement.lang).toBe('en');
  expect(document.documentElement.dataset.theme).toBe('cosmic-dark');
  expect(document.documentElement.classList.contains('dark')).toBe(true);
  expect(document.querySelector<HTMLImageElement>('img[alt="GitCosmos"]')!.src).toContain('Logo_GitCosmos.png');
  expect(document.body.textContent).toContain('Your universe starts here');
  const themeButton = document.querySelector<HTMLButtonElement>('button[aria-pressed]')!;
  themeButton.click(); await flushPromises();
  expect(document.documentElement.dataset.theme).toBe('cosmic-light');
  expect(document.documentElement.classList.contains('dark')).toBe(false);
  expect(localStorage.getItem('gitcosmos.theme')).toBe('light');
  const selector = document.querySelector('select')!;
  selector.value = 'es'; selector.dispatchEvent(new Event('change', { bubbles: true }));
  await flushPromises();
  expect(document.body.textContent).toContain('Tu universo comienza aquí');
  expect(fetch).toHaveBeenLastCalledWith('/api/System/GetStatus', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  vi.mocked(fetch).mockImplementation(async () => new Response(JSON.stringify({
    success: true, result: { items: [], page: 0, pageSize: 10, totalRows: 0, totalPages: 0 }, messages: [], errors: [],
  })));
  await router.push('/workspaces'); await flushPromises();
  expect(document.querySelector('h1')!.textContent).toBe('Workspaces');
  expect(document.body.textContent).toContain('Aún no tiene workspaces');
  app.unmount(); vi.unstubAllGlobals(); localStorage.clear();
});
