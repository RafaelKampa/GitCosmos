import { afterEach, expect, it, vi } from 'vitest';
import { RepositoryChangesApiService } from './repository-changes-api.service';

afterEach(() => vi.unstubAllGlobals());

it('encodes path and area as query data with workspace, repository, language and cancellation', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ success: true, result: {}, errors: [], messages: [] })));
  vi.stubGlobal('fetch', fetchMock);
  const timeout = vi.spyOn(AbortSignal, 'timeout');
  const api = new RepositoryChangesApiService(() => 'es', key => key);
  const controller = new AbortController();
  await api.getChanges(1, 2, controller.signal);
  expect(fetchMock).toHaveBeenLastCalledWith('/api/RepositoryChanges/GetChanges?workspaceId=1&repositoryId=2', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  await api.getDiff(1, 2, { path: 'folder/file ç + #.txt', area: 'staged' }, controller.signal);
  const url = new URL(fetchMock.mock.calls[1]![0] as string, 'http://localhost');
  expect(url.searchParams.get('path')).toBe('folder/file ç + #.txt'); expect(url.searchParams.get('area')).toBe('staged');
  expect(url.pathname).toBe('/api/RepositoryChanges/GetDiff');
  expect(timeout).not.toHaveBeenCalled();
  controller.abort(); expect(fetchMock.mock.calls[1]![1].signal.aborted).toBe(true);
  await api.getChanges(1, 2); await api.getDiff(1, 2, { path: 'file', area: 'working' });
  fetchMock.mockImplementation(async () => new Response(JSON.stringify({ success: false, errors: ['Arquivo mudou'] }), { status: 409 }));
  await expect(api.getDiff(1, 2, { path: 'file', area: 'working' })).rejects.toMatchObject({ status: 409, message: 'Arquivo mudou' });
});
