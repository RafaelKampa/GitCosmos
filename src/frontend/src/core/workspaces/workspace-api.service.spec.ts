import { afterEach, expect, it, vi } from 'vitest';
import { WorkspaceApiService } from './workspace-api.service';

afterEach(() => vi.unstubAllGlobals());

it('sends pagination and a JSON creation request through the localized client', async () => {
  const response = { success: true, result: { id: 1 }, errors: [], messages: [] };
  const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(response)));
  vi.stubGlobal('fetch', fetchMock);
  const api = new WorkspaceApiService(() => 'en', key => key);
  const source = new AbortController();
  expect(await api.getWorkspaces(2, source.signal)).toEqual(response);
  expect(fetchMock).toHaveBeenLastCalledWith('/api/Workspace/GetAllWorkspaces?page=2&pageSize=10',
    expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
  fetchMock.mockResolvedValue(new Response(JSON.stringify(response)));
  expect(await api.createWorkspace({ name: 'Team' }, source.signal)).toEqual(response);
  expect(fetchMock).toHaveBeenLastCalledWith('/api/Workspace/CreateWorkspace', expect.objectContaining({
    method: 'POST', body: '{"name":"Team"}',
    headers: { 'Accept-Language': 'en', 'Content-Type': 'application/json' },
  }));
});

it('sends localized PUT and DELETE requests with their payload, query and cancellation', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({
    success: true, result: 4, errors: [], messages: ['Done'],
  })));
  vi.stubGlobal('fetch', fetchMock);
  const api = new WorkspaceApiService(() => 'es', key => key);
  const controller = new AbortController();
  await api.updateWorkspace({ id: 4, name: 'New Team' }, controller.signal);
  expect(fetchMock).toHaveBeenLastCalledWith('/api/Workspace/UpdateWorkspace', expect.objectContaining({
    method: 'PUT', body: '{"id":4,"name":"New Team"}',
    headers: { 'Accept-Language': 'es', 'Content-Type': 'application/json' },
  }));
  await api.deleteWorkspace(4, controller.signal);
  expect(fetchMock).toHaveBeenLastCalledWith('/api/Workspace/DeleteWorkspace?id=4', expect.objectContaining({
    method: 'DELETE', headers: { 'Accept-Language': 'es' },
  }));
  const signal = fetchMock.mock.calls[1]![1].signal as AbortSignal;
  controller.abort();
  expect(signal.aborted).toBe(true);
});

it('preserves localized conflicts and missing-workspace errors during mutations', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({
    success: false, errors: ['Localized error'], traceId: 'mutation-trace',
  }), { status: 409 }));
  vi.stubGlobal('fetch', fetchMock);
  const api = new WorkspaceApiService(() => 'en', key => key);
  await expect(api.updateWorkspace({ id: 4, name: 'Duplicate' })).rejects.toMatchObject({ status: 409, message: 'Localized error', traceId: 'mutation-trace' });
  fetchMock.mockImplementation(async () => new Response(JSON.stringify({ success: false, errors: ['Not found'] }), { status: 404 }));
  await expect(api.deleteWorkspace(4)).rejects.toMatchObject({ status: 404, message: 'Not found' });
});
