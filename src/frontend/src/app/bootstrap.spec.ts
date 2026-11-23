import { afterEach, expect, it, vi } from 'vitest';
import { flushPromises } from '@vue/test-utils';
import type { App } from 'vue';
let app: App | undefined;
afterEach(() => { app?.unmount(); app = undefined; vi.unstubAllGlobals(); localStorage.clear(); document.body.replaceChildren(); window.history.replaceState({}, '', '/'); });

it('boots Cosmic Explorer, preserves branding, switches themes and languages and loads each real route', async () => {
  localStorage.clear(); localStorage.setItem('gitcosmos.language', 'en'); localStorage.setItem('gitcosmos.theme', 'dark');
  window.history.replaceState({}, '', '/'); document.body.innerHTML = '<div id="app"></div>';
  const status = { branch: 'main', isDetached: false, hasCommits: true, changedFiles: 0, modified: 0, added: 0, deleted: 0, renamed: 0, untracked: 0, conflicted: 0, staged: 0, unstaged: 0 };
  const catalog = { workspaces: [{ id: 1, name: 'Team' }], repositories: [{ id: 2, name: 'Repo', localPath: 'C:/repo', workspaceIds: [1], state: 'clean', status, error: null }] };
  const fetchMock = vi.fn().mockImplementation(async (url: string) => {
    let result: unknown = catalog; let messages: string[] = [];
    if (url.includes('GetAllWorkspaces')) result = { items: [], page: 0, pageSize: 10, totalRows: 0, totalPages: 0 };
    if (url.includes('GetDashboard')) result = { workspaceId: 1, name: 'Team', checkedAt: '2026-11-22T12:00:00Z', repositories: [] };
    if (url.includes('GetChanges')) result = { id: 2, name: 'Repo details', localPath: 'C:/repo', status, files: [] };
    if (url.includes('GetStatus')) { result = { name: 'GitCosmos' }; messages = ['Ready']; }
    return new Response(JSON.stringify({ success: true, errors: [], messages, result }));
  });
  vi.stubGlobal('fetch', fetchMock);
  app = (await import('../main')).app;
  const { router } = await import('./router'); await router.isReady(); await flushPromises();
  expect(document.querySelector('h1')!.textContent).toBe('Mission Control');
  expect(document.querySelector<HTMLImageElement>('img[alt="GitCosmos"]')!.src).toContain('Logo_GitCosmos.png');
  expect(document.documentElement.dataset.theme).toBe('cosmic-dark');
  document.querySelector<HTMLButtonElement>('.theme-switcher')!.click(); await flushPromises();
  expect(document.documentElement.dataset.theme).toBe('cosmic-light');
  expect(localStorage.getItem('gitcosmos.theme')).toBe('light');
  const selector = document.querySelector<HTMLSelectElement>('.language-switcher select')!;
  selector.value = 'es'; selector.dispatchEvent(new Event('change', { bubbles: true })); await flushPromises();
  expect(document.documentElement.lang).toBe('es');
  expect(document.querySelector('.explorer-sidebar')!.textContent).toContain('Sin workspace');
  await router.push('/workspaces'); await flushPromises(); expect(document.querySelector('h1')!.textContent).toBe('Workspaces');
  await router.push('/workspaces/1'); await flushPromises(); expect(document.querySelector('h1')!.textContent).toBe('Team');
  await router.push('/workspaces/1/repositories/2'); await flushPromises(); expect(document.querySelector('h1')!.textContent).toBe('Repo details');
  await router.push('/repositories'); await flushPromises(); expect(document.querySelector('h1')!.textContent).toBe('Todos los repositorios');
  await router.push('/repositories/2'); await flushPromises(); expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/api/RepositoryCatalog/GetChanges?'), expect.any(Object));
  await router.push('/settings'); await flushPromises(); expect(document.querySelector('h1')!.textContent).toBe('Configuración');
  await router.push('/connection'); await flushPromises(); expect(document.body.textContent).toContain('Tu universo comienza aquí');
  expect(fetchMock).toHaveBeenCalledWith('/api/System/GetStatus', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
}, 60_000);