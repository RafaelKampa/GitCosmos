import { afterEach, expect, it, vi } from 'vitest';
import { RepositoryLinesApiService } from './repository-lines-api.service';
afterEach(() => vi.unstubAllGlobals());
it('sends scoped paths, line IDs and both review tokens with the selected language', async () => {
  const fetch = vi.fn(async () => new Response(JSON.stringify({ success: true, result: true, errors: [], messages: [] }))); vi.stubGlobal('fetch', fetch);
  const api = new RepositoryLinesApiService(() => 'es', () => 'error'); const file = { repositoryId: 2, workspaceId: 1, path: 'folder/a & b.txt', area: 'working' as const }; const controller = new AbortController();
  await api.read(file, controller.signal); const [url, options] = fetch.mock.calls[0]! as unknown as [string, RequestInit]; expect(new URL(url, 'http://localhost').searchParams.get('path')).toBe(file.path); expect(options.headers).toEqual({ 'Accept-Language': 'es' });
  const request = { file, lines: [5, 9], token: 'snapshot' }; await api.prepare(request); await api.execute(request, 'reviewed');
  expect(fetch).toHaveBeenLastCalledWith('/api/RepositoryLines/Execute', expect.objectContaining({ body: JSON.stringify({ request, token: 'reviewed' }) }));
});
