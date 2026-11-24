import { afterEach, expect, it, vi } from 'vitest';
import { RepositoryEditApiService } from './repository-edit-api.service';
afterEach(() => vi.unstubAllGlobals());
it('posts scoped preflight and execution tokens with language and cancellation', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ success: true, result: {}, errors: [], messages: [] })));
  vi.stubGlobal('fetch', fetchMock); const source = new AbortController();
  const api = new RepositoryEditApiService(() => 'es', () => 'error');
  const request = { repositoryId: 1, workspaceId: 2, operation: 'stage' as const, paths: ['[file] ç.txt'], subject: '', description: '', amend: false, push: false };
  await api.prepare(request, source.signal); await api.execute(request, 'token', source.signal);
  expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/RepositoryEdit/Prepare', expect.objectContaining({ method: 'POST', body: JSON.stringify(request), headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' } }));
  expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/RepositoryEdit/Execute', expect.objectContaining({ body: JSON.stringify({ request, token: 'token' }) }));
  source.abort(); expect((fetchMock.mock.calls[1]![1] as RequestInit).signal!.aborted).toBe(true);
});
