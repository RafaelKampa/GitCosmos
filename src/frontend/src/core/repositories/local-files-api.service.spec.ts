import { afterEach, expect, it, vi } from 'vitest';
import { LocalFilesApiService } from './local-files-api.service';
afterEach(() => vi.unstubAllGlobals());
it('sends scoped paths, area and reviewed operations through the domain service with the current language', async () => {
  const fetch = vi.fn(async () => new Response(JSON.stringify({ success: true, result: true, errors: [], messages: [] }))); vi.stubGlobal('fetch', fetch);
  const api = new LocalFilesApiService(() => 'es', () => 'error'); const request = { repositoryId: 2, workspaceId: 1, paths: ['a & b.cs'], area: 'working' as const, action: 'info' }; const controller = new AbortController();
  await api.read(request, controller.signal); await api.prepare({ ...request, action: 'discard' }); await api.execute({ ...request, action: 'discard', token: 'review' }); await api.desktop({ ...request, action: 'line', tool: 'rider', start: 12, end: 12 });
  expect(fetch).toHaveBeenCalledTimes(4); expect(fetch).toHaveBeenLastCalledWith('/api/LocalFiles/Desktop', expect.objectContaining({ headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' }, body: JSON.stringify({ ...request, action: 'line', tool: 'rider', start: 12, end: 12 }) }));
});
