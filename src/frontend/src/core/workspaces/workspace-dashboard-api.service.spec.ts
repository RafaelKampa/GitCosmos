import { afterEach, expect, it, vi } from 'vitest';
import { WorkspaceDashboardApiService } from './workspace-dashboard-api.service';

afterEach(() => vi.unstubAllGlobals());

it('requests an authorized workspace with the current language, cancellation and no fixed request timeout', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ success: true, result: {}, messages: [], errors: [] })));
  vi.stubGlobal('fetch', fetchMock);
  const timeout = vi.spyOn(AbortSignal, 'timeout');
  const api = new WorkspaceDashboardApiService(() => 'es', key => key);
  const controller = new AbortController();
  await api.getDashboard(3, controller.signal);
  expect(fetchMock).toHaveBeenCalledWith('/api/WorkspaceDashboard/GetDashboard?workspaceId=3',
    expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  expect(timeout).not.toHaveBeenCalled();
  controller.abort(); expect(fetchMock.mock.calls[0]![1].signal.aborted).toBe(true);
  await api.getDashboard(3);
  fetchMock.mockImplementation(async () => new Response(JSON.stringify({ success: false, errors: ['Workspace missing'] }), { status: 404 }));
  await expect(api.getDashboard(9)).rejects.toMatchObject({ status: 404, message: 'Workspace missing' });
});
