import { afterEach, expect, it, vi } from 'vitest';
import { LocalFolderApiService } from './local-folder-api.service';
afterEach(() => vi.unstubAllGlobals());

it('supports cancellation without an automatic HTTP timeout', async () => {
  const timeout = vi.spyOn(AbortSignal, 'timeout');
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ success: true, result: { localPath: null, cancelled: true } })));
  vi.stubGlobal('fetch', fetchMock);
  const api = new LocalFolderApiService(() => 'en', key => key);
  await api.selectFolder({ initialPath: null });
  expect(timeout).not.toHaveBeenCalled();
  expect((fetchMock.mock.calls[0]![1].signal as AbortSignal).aborted).toBe(false);
  const controller = new AbortController();
  await api.selectFolder({ initialPath: 'C:\\repo' }, controller.signal);
  controller.abort();
  expect((fetchMock.mock.calls[1]![1].signal as AbortSignal).aborted).toBe(true);
  fetchMock.mockRejectedValue(new Error('network'));
  await expect(api.selectFolder({ initialPath: null })).rejects.toThrow('networkError');
  await expect(api.selectFolder({ initialPath: null }, controller.signal)).rejects.toThrow('cancelled');
});
