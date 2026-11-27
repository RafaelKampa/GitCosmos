import { afterEach, expect, it, vi } from 'vitest';
import { RepositoryConflictApiService } from './repository-conflict-api.service';
afterEach(() => vi.unstubAllGlobals());
it('sends scoped paths and explicit reviewed resolutions with the selected language', async () => {
  const fetch = vi.fn(async () => new Response(JSON.stringify({ success: true, result: true, errors: [], messages: [] }))); vi.stubGlobal('fetch', fetch);
  const api = new RepositoryConflictApiService(() => 'es', () => 'error'); const file = { repositoryId: 2, workspaceId: 1, path: 'folder/a & b.txt' }; const controller = new AbortController();
  await api.read(file, controller.signal); const [url, options] = fetch.mock.calls[0]! as unknown as [string, RequestInit]; expect(new URL(url, 'http://localhost').searchParams.get('path')).toBe(file.path); expect(options.headers).toEqual({ 'Accept-Language': 'es' });
  const request = { file, action: 'edit' as const, content: 'resolved\r\n', token: 'reviewed' };
  await api.prepare(request); await api.resolve(request); await api.external(file);
  expect(fetch).toHaveBeenLastCalledWith('/api/RepositoryConflict/External', expect.objectContaining({ body: JSON.stringify(file) }));
  expect(fetch.mock.calls.map(call => (call as unknown as [string])[0])).toContain('/api/RepositoryConflict/Resolve');
});
