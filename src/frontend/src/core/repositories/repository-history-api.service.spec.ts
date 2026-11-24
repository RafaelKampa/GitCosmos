import { afterEach, expect, it, vi } from 'vitest';
import { RepositoryHistoryApiService } from './repository-history-api.service';
afterEach(() => vi.unstubAllGlobals());

it('encodes scoped history, snapshots, commit identifiers and literal paths with cancellation and language', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ success: true, result: {}, errors: [], messages: [] })));
  vi.stubGlobal('fetch', fetchMock);
  const api = new RepositoryHistoryApiService(() => 'es', () => 'error');
  const source = new AbortController();
  await api.getHistory(0, 2, 0, null, source.signal);
  await api.getHistory(1, 2, 1, 'abc');
  await api.getCommit(1, 2, 'sha');
  await api.getDiff(0, 2, 'sha', '[a] ç&?#.txt');
  expect(fetchMock.mock.calls.map(call => call[0])).toEqual([
    '/api/RepositoryHistory/GetHistory?workspaceId=0&repositoryId=2&page=0&pageSize=50',
    '/api/RepositoryHistory/GetHistory?workspaceId=1&repositoryId=2&page=1&pageSize=50&snapshot=abc',
    '/api/RepositoryHistory/GetCommit?workspaceId=1&repositoryId=2&sha=sha',
    '/api/RepositoryHistory/GetDiff?workspaceId=0&repositoryId=2&sha=sha&path=%5Ba%5D+%C3%A7%26%3F%23.txt'
  ]);
  expect(fetchMock).toHaveBeenCalledWith(expect.any(String), expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  source.abort(); expect((fetchMock.mock.calls[0]![1] as RequestInit).signal!.aborted).toBe(true);
});
