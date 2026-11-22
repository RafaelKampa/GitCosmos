import { afterEach, expect, it, vi } from 'vitest';
import { WorkspaceRepositoryApiService } from './workspace-repository-api.service';

afterEach(() => vi.unstubAllGlobals());

it('imports immediate repositories without a fixed timeout and propagates language, payload and cancellation', async () => {
  const response = { success: true, result: { items: [] }, messages: [], errors: [] };
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(response)));
  vi.stubGlobal('fetch', fetchMock);
  const timeout = vi.spyOn(AbortSignal, 'timeout');
  const api = new WorkspaceRepositoryApiService(() => 'es', key => key);
  const controller = new AbortController();
  const request = { workspaceId: 3, localPath: 'C:/Repos/ReposTestes' };
  expect(await api.importRepositories(request, controller.signal)).toEqual(response);
  expect(fetchMock).toHaveBeenCalledWith('/api/RepositoryImport/ImportRepositories', expect.objectContaining({
    method: 'POST', body: JSON.stringify(request), headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' },
  }));
  expect(timeout).not.toHaveBeenCalled();
  controller.abort();
  expect((fetchMock.mock.calls[0]![1].signal as AbortSignal).aborted).toBe(true);
  fetchMock.mockResolvedValue(new Response(JSON.stringify(response)));
  await api.importRepositories(request);
});

it('uses the workspace id, JSON path, language and cancellation for repository requests', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({
    success: true, result: [], messages: [], errors: [],
  })));
  vi.stubGlobal('fetch', fetchMock);
  const api = new WorkspaceRepositoryApiService(() => 'es', key => key);
  const controller = new AbortController();
  await api.getRepositories(3, controller.signal);
  expect(fetchMock).toHaveBeenLastCalledWith('/api/WorkspaceRepository/GetRepositories?workspaceId=3',
    expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  const request = { workspaceId: 3, localPath: "C:\\repo espaço ' sample" };
  await api.addRepository(request, controller.signal);
  expect(fetchMock).toHaveBeenLastCalledWith('/api/WorkspaceRepository/AddRepository', expect.objectContaining({
    method: 'POST', body: JSON.stringify(request), headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' },
  }));
  await api.removeRepository(3, 8, controller.signal);
  expect(fetchMock).toHaveBeenLastCalledWith('/api/WorkspaceRepository/RemoveRepository?workspaceId=3&repositoryId=8',
    expect.objectContaining({ method: 'DELETE', headers: { 'Accept-Language': 'es' } }));
  const signal = fetchMock.mock.calls[2]![1].signal as AbortSignal;
  controller.abort(); expect(signal.aborted).toBe(true);
});

it('preserves localized repository validation, conflict and missing-link errors', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({
    success: false, errors: ['Localized validation'], traceId: 'trace-repository',
  }), { status: 400 }));
  vi.stubGlobal('fetch', fetchMock);
  const api = new WorkspaceRepositoryApiService(() => 'en', key => key);
  await expect(api.addRepository({ workspaceId: 1, localPath: 'invalid' })).rejects.toMatchObject({ status: 400, traceId: 'trace-repository', message: 'Localized validation' });
  fetchMock.mockImplementation(async () => new Response(JSON.stringify({ success: false, errors: ['Already linked'] }), { status: 409 }));
  await expect(api.addRepository({ workspaceId: 1, localPath: 'existing' })).rejects.toMatchObject({ status: 409, message: 'Already linked' });
  fetchMock.mockImplementation(async () => new Response(JSON.stringify({ success: false, errors: ['Link missing'] }), { status: 404 }));
  await expect(api.removeRepository(1, 8)).rejects.toMatchObject({ status: 404, message: 'Link missing' });
});
