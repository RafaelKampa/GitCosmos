import { afterEach, expect, it, vi } from 'vitest';
import { CatalogApiService } from './catalog-api.service';
afterEach(() => vi.unstubAllGlobals());
it('uses standalone catalog endpoints, language and cancellation without requiring a workspace', async () => {
  const fetch = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ success: true, result: {}, messages: [], errors: [] })));
  vi.stubGlobal('fetch', fetch); const api = new CatalogApiService(() => 'es', key => key); const source = new AbortController();
  await api.getExplorer(source.signal); await api.getOverview(source.signal); await api.addRepository('C:/Repo', source.signal);
  await api.preview({ workspaceId: 0, remoteUrl: 'https://example.com/repo.git', parentPath: 'C:/Repos' }, source.signal);
  await api.clone({ workspaceId: 0, remoteUrl: 'https://example.com/repo.git', parentPath: 'C:/Repos', destinationPath: 'C:/Repos/repo' }, source.signal);
  expect(fetch.mock.calls.map(call => call[0])).toEqual(['/api/RepositoryCatalog/GetExplorer','/api/RepositoryCatalog/GetOverview','/api/RepositoryCatalog/AddRepository','/api/RepositoryCatalog/Preview','/api/RepositoryCatalog/CloneRepository']);
  expect(fetch.mock.calls[2]![1].body).toBe(JSON.stringify({ localPath: 'C:/Repo' }));
  expect(fetch.mock.calls[0]![1].headers).toEqual({ 'Accept-Language': 'es' }); source.abort(); expect(fetch.mock.calls[4]![1].signal.aborted).toBe(true);
  await api.getExplorer(); await api.getOverview(); await api.addRepository('C:/Repo');
  await api.preview({ workspaceId: 0, remoteUrl: 'url', parentPath: 'parent' }); await api.clone({ workspaceId: 0, remoteUrl: 'url', parentPath: 'parent', destinationPath: 'dest' });
});
