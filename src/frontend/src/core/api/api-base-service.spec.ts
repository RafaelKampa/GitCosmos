import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiBaseService } from './api-base-service';
import { ApiError } from './api-error';

describe('API client', () => {
  const fetchMock = vi.fn();
  const api = new ApiBaseService(() => 'es', key => key);
  beforeEach(() => { vi.stubGlobal('fetch', fetchMock); fetchMock.mockReset(); });
  afterEach(() => vi.unstubAllGlobals());

  it('sends the selected language and returns the business envelope', async () => {
    const body = { success: true, result: { name: 'GitCosmos' }, errors: [], messages: ['Listo'] };
    fetchMock.mockResolvedValue(new Response(JSON.stringify(body)));
    expect(await api.get('/api/System/GetStatus')).toEqual(body);
    expect(fetchMock).toHaveBeenCalledWith('/api/System/GetStatus', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  });

  it('passes cancellation to fetch', async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ success: true, result: null })));
    const controller = new AbortController();
    await api.get('/api', controller.signal);
    const signal = fetchMock.mock.calls[0]![1].signal as AbortSignal;
    controller.abort();
    expect(signal.aborted).toBe(true);
  });

  it.each([200, 400])('preserves localized errors and trace for HTTP %s', async status => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ success: false, errors: ['Campo inválido'], traceId: 'trace-1' }), { status }));
    const error = await api.get('/api').catch(e => e as ApiError);
    expect(error).toMatchObject({ name: 'ApiError', message: 'Campo inválido', status, traceId: 'trace-1' });
  });

  it.each([{}, { errors: [] }])('uses a translated fallback for incomplete errors', async body => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify(body), { status: 500 }));
    await expect(api.get('/api')).rejects.toThrow('networkError');
  });

  it('handles network failure and invalid JSON', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('offline'));
    await expect(api.get('/api')).rejects.toThrow('networkError');
    fetchMock.mockResolvedValueOnce(new Response('<html>error</html>'));
    await expect(api.get('/api')).rejects.toThrow('networkError');
  });

  it('distinguishes timeout and user cancellation', async () => {
    const timeout = new AbortController();
    vi.spyOn(AbortSignal, 'timeout').mockReturnValue(timeout.signal);
    timeout.abort();
    fetchMock.mockRejectedValue(new DOMException('aborted', 'AbortError'));
    await expect(api.get('/api')).rejects.toThrow('timeout');
    vi.restoreAllMocks();
    const controller = new AbortController(); controller.abort();
    await expect(api.get('/api', controller.signal)).rejects.toThrow('cancelled');
  });
});
