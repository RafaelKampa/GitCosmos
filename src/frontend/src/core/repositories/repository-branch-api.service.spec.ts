import { afterEach, expect, it, vi } from 'vitest';
import { RepositoryBranchApiService } from './repository-branch-api.service';
afterEach(() => vi.unstubAllGlobals());
it('sends the stored repository scope, reviewed request and token with language and cancellation', async () => {
  const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ success: true, result: {}, messages: [], errors: [] })));
  vi.stubGlobal('fetch', fetchMock); const controller = new AbortController();
  const api = new RepositoryBranchApiService(() => 'es', () => 'error');
  const request = { repositoryId: 2, workspaceId: 1, operation: 'create' as const, name: 'main', newName: 'feature/á-new' };
  await api.getBranches(2, 1, controller.signal); await api.prepare(request, controller.signal); await api.execute(request, 'review-token', controller.signal);
  await api.getResources(2, 1, controller.signal);
  expect(fetchMock).toHaveBeenNthCalledWith(4, '/api/RepositoryBranch/GetResources?repositoryId=2&workspaceId=1', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/RepositoryBranch/GetBranches?repositoryId=2&workspaceId=1', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/RepositoryBranch/Prepare', expect.objectContaining({ method: 'POST', body: JSON.stringify(request) }));
  expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/RepositoryBranch/Execute', expect.objectContaining({ body: JSON.stringify({ request, token: 'review-token' }) }));
  controller.abort(); expect((fetchMock.mock.calls[2]![1] as RequestInit).signal!.aborted).toBe(true);
});
