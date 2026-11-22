import { afterEach, expect, it, vi } from 'vitest';
import { RepositoryCloneApiService } from './repository-clone-api.service';
afterEach(() => vi.unstubAllGlobals());
it('sends the preview and confirmed destination and allows a long-running cancellable clone', async () => {
  const timeout = vi.spyOn(AbortSignal, 'timeout');
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ success: true, result: {} })));
  vi.stubGlobal('fetch', fetchMock);
  const api = new RepositoryCloneApiService(() => 'en', key => key);
  const request = { workspaceId: 1, remoteUrl: 'https://example.com/repo.git', parentPath: 'C:\\Repos' };
  const source = new AbortController();
  await api.preview(request, source.signal);
  expect(timeout).toHaveBeenCalledWith(30_000);
  timeout.mockClear();
  await api.clone({ ...request, destinationPath: 'C:\\Repos\\repo' }, source.signal);
  expect(timeout).not.toHaveBeenCalled();
  expect(fetchMock).toHaveBeenLastCalledWith('/api/RepositoryClone/CloneRepository', expect.objectContaining({
    method: 'POST', body: JSON.stringify({ ...request, destinationPath: 'C:\\Repos\\repo' }),
  }));
  source.abort();
  expect((fetchMock.mock.calls[1]![1].signal as AbortSignal).aborted).toBe(true);
});
