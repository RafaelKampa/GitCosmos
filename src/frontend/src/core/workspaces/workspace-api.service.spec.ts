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
